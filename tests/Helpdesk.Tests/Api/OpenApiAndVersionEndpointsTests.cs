using System.Net;
using System.Text.Json;
using Helpdesk.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helpdesk.Tests.Api;

public class OpenApiAndVersionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiAndVersionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseIsolatedTestStorage();
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test",
                    ["Jwt:Key"] = "test-key-123456789012345678901234",
                    ["AppBar:BuildVersion"] = "0.0.148"
                });
            });
        });
    }

    [Fact]
    public async Task OpenApi_V1_Json_IsServed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/system/version", content, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(content);
        Assert.Equal("RatelDesk API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("info").GetProperty("version").GetString()));
        Assert.True(document.RootElement.TryGetProperty("x-tagGroups", out var groups));
        Assert.Contains(groups.EnumerateArray(), group => group.GetProperty("name").GetString() == "Ticketing");

        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal("JWT", schemes.GetProperty("JwtBearer").GetProperty("bearerFormat").GetString());
        Assert.Equal("opaque rdk credential", schemes.GetProperty("IntegrationCredential").GetProperty("bearerFormat").GetString());
        Assert.Equal("cookie", schemes.GetProperty("LocalSession").GetProperty("in").GetString());

        var integrationCredentialsEndpoint = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText?.TrimEnd('/') == "/api/v1/integration-credentials" &&
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);
        Assert.Contains(integrationCredentialsEndpoint.Metadata, metadata => metadata is Microsoft.AspNetCore.Authorization.IAuthorizeData);

        Assert.Empty(SecuritySchemes(document, "/api/v1/setup/status", "get"));
        Assert.Empty(SecuritySchemes(document, "/api/v1/tickets/public/view", "get"));
        Assert.Equal(["JwtBearer", "LocalSession"], SecuritySchemes(document, "/api/v1/integration-credentials", "get"));
        Assert.Equal(["OrchestrationM2M"], SecuritySchemes(document, "/api/v1/orchestration/provider/m2m/ping", "get"));
        Assert.Equal(["AiAgentJwt"], SecuritySchemes(document, "/api/v1/auth/ai-agent/status", "get"));
        Assert.Equal(["IntegrationCredential", "JwtBearer", "LocalSession"], SecuritySchemes(document, "/api/v1/incidents", "get"));
    }

    private static string[] SecuritySchemes(JsonDocument document, string path, string method)
    {
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty(path, out var pathItem),
            $"OpenAPI path '{path}' was not generated. Available paths: {string.Join(", ", paths.EnumerateObject().Select(item => item.Name))}");
        Assert.True(pathItem.TryGetProperty(method, out var operation),
            $"OpenAPI operation '{method}' was not generated for path '{path}'.");
        return operation.TryGetProperty("security", out var security)
            ? security.EnumerateArray()
                .SelectMany(requirement => requirement.EnumerateObject().Select(property => property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];
    }

}
