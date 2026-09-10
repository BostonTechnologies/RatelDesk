using System.Text.Json.Nodes;
using Helpdesk.AgentClient;
using Helpdesk.Mcp.Configuration;
using Helpdesk.Mcp.Resources;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Mcp;

public sealed class HelpdeskResourcesTests
{
    [Fact]
    public void Status_and_redacted_configuration_include_the_target_without_secrets()
    {
        var client = Substitute.For<IHelpdeskAgentClient>();
        client.Configuration.Returns(new AgentClientConfiguration("https://prod.example", "https://auth.example/token", "client", "agent", "secret", "scope", null));
        var resources = new HelpdeskResources(client, new HelpdeskMcpTarget("prod", new Uri("https://prod.example")));

        var status = JsonNode.Parse(resources.ServerStatus())!.AsObject();
        var config = JsonNode.Parse(resources.RedactedConfig())!.AsObject();

        Assert.Equal("prod", status["target"]?["instance"]?.ToString());
        Assert.Equal("https://prod.example", status["target"]?["apiBaseUrl"]?.ToString());
        Assert.Equal("prod", config["target"]?["instance"]?.ToString());
        Assert.DoesNotContain("secret", config.ToJsonString(), StringComparison.Ordinal);
    }
}
