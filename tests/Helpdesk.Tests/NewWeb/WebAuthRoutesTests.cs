extern alias NewWeb;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using NewWeb::HelpDesk.NewWeb;
using NewWeb::HelpDesk.NewWeb.Services;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Notification;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class WebAuthRoutesTests
{
    [Fact]
    public async Task Login_Page_Shows_Provider_Neutral_Authentik_Branding()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("RatelDesk", content, StringComparison.Ordinal);
        Assert.Contains("Self-hosted", content, StringComparison.Ordinal);
        Assert.Contains("Continue to RatelDesk using your authorised account.", content, StringComparison.Ordinal);
        Assert.Contains("Sign in to RatelDesk", content, StringComparison.Ordinal);
        Assert.Contains("href=\"/login-authentik\"", content, StringComparison.Ordinal);
        Assert.Contains("rateldesk-mark.webp", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Automation Platform", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<header", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Developer account tools", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_Portal_Shows_EndUser_Actions_And_Admin_Link()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Welcome to RatelDesk", content, StringComparison.Ordinal);
        Assert.Contains("Submit a New Ticket", content, StringComparison.Ordinal);
        Assert.Contains("View Existing Tickets", content, StringComparison.Ordinal);
        Assert.Contains("RatelDesk", content, StringComparison.Ordinal);
        Assert.Contains("rateldesk-mark.webp", content, StringComparison.Ordinal);
        Assert.Contains("href=\"/login\"", content, StringComparison.Ordinal);
        Assert.Contains("Admin sign-in", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<header", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authenticated_User_Visiting_Login_Redirects_To_Home()
    {
        using var factory = CreateFactory(enableTestAuth: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/home", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Legacy_Account_Login_Redirects_To_Login()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Api_Shorthand_Redirects_To_WebHosted_Scalar_Docs()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/api/docs/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_Authentik_Challenges_Authentik_Oidc()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/login-authentik");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("https://id.example.com/application/o/rateldesk/authorize",
            response.Headers.Location!.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Login_Page_Includes_The_Published_Blazor_Bootstrap_Asset()
    {
        var component = File.ReadAllText(Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "Components",
            "App.razor"));
        var project = File.ReadAllText(Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "HelpDesk.NewWeb.csproj"));

        Assert.Contains("<script src=@Assets[\"_framework/blazor.web.js\"]></script>", component, StringComparison.Ordinal);
        Assert.Contains("<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void WebHost_Fails_Startup_When_Authentik_Client_Is_Not_Configured()
    {
        using var factory = CreateFactory(useAzureFallback: true);

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(ex.Failures, failure => failure.Contains("Authentication:Authentik:ClientId", StringComparison.Ordinal));
        Assert.Contains(ex.Failures, failure => failure.Contains("AUTHENTIK_CLIENT_SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Anonymous_User_Is_Challenged_By_Authentik_For_Authorized_Page()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/auth");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("https://id.example.com/application/o/rateldesk/authorize",
            response.Headers.Location!.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authenticated_User_Can_Open_Authorized_Page()
    {
        using var factory = CreateFactory(enableTestAuth: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.GetAsync("/auth");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Authentication Dashboard", content, StringComparison.Ordinal);
        Assert.Contains("stub-access-token", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_Detail_Does_Not_Call_Api_During_Production_Prerender()
    {
        using var factory = CreateFactory(enableTestAuth: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.GetAsync("/changes/change-1");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("blazor.web.js", content, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred while processing your request", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_List_Does_Not_Throw_When_Lifecycle_Query_Is_Absent()
    {
        using var factory = CreateFactory(enableTestAuth: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.GetAsync("/changes");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Changes", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Querystring values cannot be parsed", content, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred while processing your request", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authenticated_User_Without_Admin_Role_Gets_Forbidden_For_Admin_Page()
    {
        using var factory = CreateFactory(enableTestAuth: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "Technician");

        var response = await client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplicationFactory<TokenService> CreateFactory(bool enableTestAuth = false, bool useAzureFallback = false)
    {
        return new WebApplicationFactory<TokenService>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                var settings = useAzureFallback
                    ? new Dictionary<string, string?>
                    {
                        ["AZURE_CLIENT_SECRET"] = "azure-client-secret",
                        ["Authentication:Authentik:Authority"] = "https://id.example.com/application/o/rateldesk/",
                        ["Authentication:Azure:Authority"] = "https://login.microsoftonline.com/tenant/v2.0",
                        ["Authentication:Azure:ClientId"] = "azure-client-id",
                        ["Authentication:Azure:ApiScope"] = "api://helpdesk/access_as_user",
                        ["Authentication:Azure:CallbackPath"] = "/signin-azure",
                        ["Authentication:Azure:SignedOutCallbackPath"] = "/signout-azure",
                        ["ApiBaseUrl"] = "https://helpdesk-api.test/"
                    }
                    : new Dictionary<string, string?>
                    {
                        ["AUTHENTIK_CLIENT_SECRET"] = "client-secret",
                        ["Authentication:Authentik:Authority"] = "https://id.example.com/application/o/rateldesk/",
                        ["Authentication:Authentik:ClientId"] = "client-id",
                        ["Authentication:Authentik:ApiScope"] = "helpdesk-api",
                        ["Authentication:Authentik:CallbackPath"] = "/signin-authentik",
                        ["Authentication:Authentik:SignedOutCallbackPath"] = "/signout-authentik",
                        ["ApiBaseUrl"] = "https://helpdesk-api.test/"
                    };

                cfg.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                services.PostConfigure<OpenIdConnectOptions>("Authentik", options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = "https://id.example.com/application/o/rateldesk/authorize",
                        EndSessionEndpoint = "https://id.example.com/application/o/rateldesk/end-session"
                    };

                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });

                if (!enableTestAuth)
                    return;

                services.AddSingleton<ITokenService, StubTokenService>();
                services.AddSingleton<ISystemNotificationApiClient, StubSystemNotificationApiClient>();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, NewWebTestAuthHandler>("Test", _ => { });
            });
        });
    }

    private sealed class StubTokenService : ITokenService
    {
        public Task<string?> GetValidAccessTokenAsync() => Task.FromResult<string?>("stub-access-token");

        public Task<bool> TryRefreshSessionAsync(HttpContext ctx, AuthenticateResult auth, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubSystemNotificationApiClient : ISystemNotificationApiClient
    {
        public Task<PagedResponse<NotificationDto>> GetPagedNotificationsAsync(
            int page = 1,
            int pageSize = 10,
            string? searchTerm = null,
            NotificationSeverity? severity = null,
            string? source = null,
            string? category = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResponse<NotificationDto> { Page = page, PageSize = pageSize });

        public Task<List<NotificationDto>> GetNotificationsAsync(
            int page = 1,
            int pageSize = 10,
            string? searchTerm = null,
            NotificationSeverity? severity = null,
            string? source = null,
            string? category = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null)
            => Task.FromResult(new List<NotificationDto>());

        public Task<List<NotificationDto>> GetUnreadNotificationsAsync(int page = 1, int pageSize = 10)
            => Task.FromResult(new List<NotificationDto>());

        public Task<List<NotificationDto>> GetUnreadErrorNotificationsAsync(int take = 20)
            => Task.FromResult(new List<NotificationDto>());

        public Task<List<NotificationDto>> GetTimelineAsync(
            string? reference = null,
            string? correlationId = null,
            string? tenantId = null,
            int take = 200)
            => Task.FromResult(new List<NotificationDto>());

        public Task<NotificationDto?> GetByIdAsync(Guid id)
            => Task.FromResult<NotificationDto?>(null);

        public Task<NotificationSummaryDto> GetSummaryAsync()
            => Task.FromResult(new NotificationSummaryDto());

        public Task<NotificationSummaryDto> GetErrorSummaryAsync()
            => Task.FromResult(new NotificationSummaryDto());

        public Task MarkReadAsync(Guid id) => Task.CompletedTask;

        public Task<int> MarkReadBulkAsync(IEnumerable<Guid> ids) => Task.FromResult(0);

        public Task<int> PurgeAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NewWebTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public NewWebTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header))
            {
                return Task.FromResult(AuthenticateResult.Fail("No authorization header"));
            }

            var role = header.ToString().Contains("HelpdeskAdmin", StringComparison.OrdinalIgnoreCase)
                ? "HelpdeskAdmin"
                : "Technician";

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Test User"),
                new(ClaimTypes.Role, role),
                new("preferred_username", "test.user@example.com")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
