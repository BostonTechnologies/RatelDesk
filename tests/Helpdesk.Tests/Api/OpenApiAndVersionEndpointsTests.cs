using System.Net;
using System.Net.Http.Json;
using Helpdesk.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Helpdesk.Tests.Api;

public class OpenApiAndVersionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiAndVersionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test",
                    ["Jwt:Key"] = "test-key-123456789012345678901234"
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
    }

    [Fact]
    public async Task System_Version_ReturnsReleaseInfo()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<SystemVersionResponse>("/api/v1/system/version");

        Assert.NotNull(response);
        Assert.Equal("Helpdesk.API", response!.ServiceName);
        Assert.Equal("v0.1.0", response.DisplayVersion);
        Assert.Equal("0.1.0", response.InformationalVersion);
        Assert.False(string.IsNullOrWhiteSpace(response.AssemblyVersion));
        Assert.Equal("Development", response.Environment);
    }

    private sealed record SystemVersionResponse(
        string ServiceName,
        string DisplayVersion,
        string InformationalVersion,
        string AssemblyVersion,
        string Environment);
}
