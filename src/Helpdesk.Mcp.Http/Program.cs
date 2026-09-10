using Helpdesk.AgentClient;
using Helpdesk.Mcp;
using Helpdesk.Mcp.Configuration;
using Helpdesk.Mcp.Http.Authorization;
using Helpdesk.Mcp.Http.Configuration;
using Helpdesk.Mcp.Http.Observability;
using Helpdesk.Mcp.Prompts;
using Helpdesk.Mcp.Resources;
using Helpdesk.Mcp.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace Helpdesk.Mcp.Http;

public sealed partial class Program
{
    public static void Main(string[] args)
    {
        const string McpScheme = "HelpdeskMcp";
        const string JwtScheme = "HelpdeskMcpJwt";
        const string PolicyName = "HelpdeskMcpRemote";

        var builder = WebApplication.CreateBuilder(args);
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        var httpOptions = builder.Configuration.GetSection(HelpdeskMcpHttpOptions.SectionName).Get<HelpdeskMcpHttpOptions>() ?? new HelpdeskMcpHttpOptions();
        var configurationPath = string.IsNullOrWhiteSpace(httpOptions.ConfigurationPath)
            ? Environment.GetEnvironmentVariable(HelpdeskMcpTarget.ConfigurationEnvironmentVariable)
            : httpOptions.ConfigurationPath;
        var agentConfiguration = AgentClientConfigurationResolver.LoadIsolated(configurationPath);
        var target = HelpdeskMcpTarget.Resolve(agentConfiguration, configurationPath, httpOptions.Instance, httpOptions.ExpectedApiBaseUrl);

        builder.Services.AddOptions<HelpdeskMcpHttpOptions>()
            .Bind(builder.Configuration.GetSection(HelpdeskMcpHttpOptions.SectionName))
            .Validate(options => AuthentikMcpOptionsValidator.IsCanonicalMcpResource(options.PublicResourceUri), "Helpdesk:Mcp:PublicResourceUri must be an absolute HTTPS /mcp URI.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AuthentikMcpOptions>, AuthentikMcpOptionsValidator>();
        builder.Services.AddOptions<AuthentikMcpOptions>()
            .Bind(builder.Configuration.GetSection(AuthentikMcpOptions.SectionName))
            .ValidateOnStart();

        var authOptions = builder.Configuration.GetSection(AuthentikMcpOptions.SectionName).Get<AuthentikMcpOptions>() ?? new AuthentikMcpOptions();
        var authorizationServer = $"{authOptions.Authority.TrimEnd('/')}/";
        var hostContext = target.ToHostContext("http", httpOptions.PublicResourceUri);
        var resourceMetadataUri = new Uri(new Uri(hostContext.ResourceUri), "/.well-known/oauth-protected-resource/mcp");
        var protectedResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = hostContext.ResourceUri,
            AuthorizationServers = [authorizationServer],
            ScopesSupported = authOptions.RequiredScopes,
            BearerMethodsSupported = ["header"]
        };
        IServiceProvider? applicationServices = null;
        var resourceTarget = new HelpdeskResources(
            () => applicationServices?.GetRequiredService<IHelpdeskAgentClient>()
                ?? throw new InvalidOperationException("Helpdesk MCP resources were invoked before the application service provider was available."),
            hostContext);

        builder.Services.AddHelpdeskMcpCore(agentConfiguration, new ReadOnlyHelpdeskMcpConfigurationSurface(agentConfiguration), hostContext);
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = McpScheme;
            options.DefaultChallengeScheme = McpScheme;
        })
            .AddJwtBearer(JwtScheme, options =>
            {
                options.Authority = authOptions.Authority;
                options.Audience = authOptions.Audience;
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Authority.TrimEnd('/'),
                    ValidateAudience = true,
                    ValidAudience = authOptions.Audience.TrimEnd('/'),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            })
            .AddMcp(McpScheme, "Helpdesk MCP", options =>
            {
                options.ForwardAuthenticate = JwtScheme;
                options.ResourceMetadataUri = resourceMetadataUri;
                options.ResourceMetadata = protectedResourceMetadata;
            });
        builder.Services.AddSingleton<IAuthorizationHandler, RequiredMcpClaimsHandler>();
        builder.Services.AddAuthorization(options => options.AddPolicy(PolicyName, policy =>
        {
            policy.AddAuthenticationSchemes(McpScheme);
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new RequiredMcpClaimsRequirement(
                authOptions.RequiredScopes.ToHashSet(StringComparer.Ordinal),
                authOptions.RequiredGroups.ToHashSet(StringComparer.Ordinal)));
        }));
        builder.Services.AddRateLimiter(options => options.AddConcurrencyLimiter("HelpdeskMcp", limiter =>
        {
            limiter.PermitLimit = 32;
            limiter.QueueLimit = 64;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }));
        builder.Services.AddSingleton<McpToolInvocationAudit>();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools(HelpdeskToolDefinitions.Create())
            .WithResources(resourceTarget)
            .WithPromptsFromAssembly(typeof(HelpdeskPrompts).Assembly)
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => (context, cancellationToken) =>
            {
                var services = context.Services ?? throw new InvalidOperationException("MCP request context does not have a service provider.");
                return services.GetRequiredService<McpToolInvocationAudit>().InvokeAsync(next, context, cancellationToken);
            }))
            .AddAuthorizationFilters();

        var app = builder.Build();
        applicationServices = app.Services;
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp") && context.Request.Headers.Origin is { Count: > 0 } origin &&
                !httpOptions.AllowedOrigins.Contains(origin.ToString(), StringComparer.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready");
        app.MapGet("/.well-known/oauth-protected-resource/mcp", () => Results.Json(protectedResourceMetadata)).AllowAnonymous();
        app.MapMcp("/mcp").RequireAuthorization(PolicyName).RequireRateLimiting("HelpdeskMcp");
        app.Run();
    }
}
