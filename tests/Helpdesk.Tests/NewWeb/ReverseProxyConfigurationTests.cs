using System.Text.Json;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class ReverseProxyConfigurationTests
{
    [Fact]
    public async Task ProductionDefaults_RouteApiProxyToLocalApiListener()
    {
        var path = Path.Combine(
            TestEnvironment.RepositoryRoot,
            "src",
            "HelpDesk.NewWeb",
            "appsettings.json");
        await using var stream = File.OpenRead(path);

        using var document = await JsonDocument.ParseAsync(stream);
        var address = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Clusters")
            .GetProperty("apiCluster")
            .GetProperty("Destinations")
            .GetProperty("api1")
            .GetProperty("Address")
            .GetString();

        Assert.Equal("http://127.0.0.1:8222/", address);
    }
}
