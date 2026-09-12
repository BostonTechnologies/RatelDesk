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
    }

    [Fact]
    public async Task System_Version_ReturnsReleaseInfo()
    {
        using var client = _factory.CreateClient();

        var httpResponse = await client.GetAsync("/api/v1/system/version");
        var content = await httpResponse.Content.ReadAsStringAsync();
        var response = await httpResponse.Content.ReadFromJsonAsync<SystemVersionResponse>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(response);
        Assert.Equal("0.1.0-rc.4", response!.Version);
        Assert.Equal("Helpdesk.API", response.AssemblyName);
        Assert.False(string.IsNullOrWhiteSpace(response.CommitHash));
        Assert.NotEqual("unknown", response.BuildTimestamp);
        Assert.Equal("Development", response.Environment);
        Assert.DoesNotContain("displayVersion", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("informationalVersion", content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SystemVersionResponse(
        string Version,
        string CommitHash,
        string BuildTimestamp,
        string AssemblyName,
        string Environment);
}
