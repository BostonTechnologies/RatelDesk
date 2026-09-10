using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Helpdesk.Tests;

public class ServiceDefaultsTests
{
    [Fact]
    public void ResolveServiceAttributes_UsesOtelEnvironmentShape()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
            ApplicationName = "fallback-helpdesk"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "helpdesk-api-dev",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.namespace=helpdesk,deployment.environment=dev,host.name=host.example.com,client.name=rateldesk"
        });

        var attributes = Extensions.ResolveServiceAttributes(builder);

        Assert.Equal("helpdesk-api-dev", attributes.ServiceName);
        Assert.Equal("helpdesk", attributes.ServiceNamespace);
        Assert.Equal("dev", attributes.DeploymentEnvironment);
        Assert.Equal("host.example.com", attributes.HostName);
        Assert.Equal("rateldesk", attributes.ClientName);
    }
}
