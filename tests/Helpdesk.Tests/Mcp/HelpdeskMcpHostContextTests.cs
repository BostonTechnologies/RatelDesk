using System.Text.Json;
using System.Text.Json.Nodes;
using Helpdesk.AgentClient;
using Helpdesk.Mcp.Configuration;
using Helpdesk.Mcp.Resources;
using Helpdesk.Mcp.Tools;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Mcp;

public sealed class HelpdeskMcpHostContextTests
{
    [Fact]
    public async Task Remote_host_context_is_returned_by_resources_and_tool_responses()
    {
        var client = Substitute.For<IHelpdeskAgentClient>();
        client.Configuration.Returns(AgentClientConfiguration.Empty);
        var context = new HelpdeskMcpHostContext(
            "prod",
            "streamable-http",
            "https://PROD.example/api/",
            HelpdeskMcpHostContext.DefaultResourceUri,
            McpOperationCatalog.Revision);
        var resources = new HelpdeskResources(client, context);
        var tools = new HelpdeskTools(client, new ReadOnlyHelpdeskMcpConfigurationSurface(AgentClientConfiguration.Empty), context);

        var status = JsonNode.Parse(resources.ServerStatus())!.AsObject();
        var response = await tools.helpdesk_incidents("state", Request("""{"incidentId":"INC-123","newState":"Resolved"}"""));

        Assert.Equal("streamable-http", status["hostContext"]?["transport"]?.ToString());
        Assert.Equal("https://prod.example/api", status["hostContext"]?["apiBaseUrl"]?.ToString());
        Assert.Equal(HelpdeskMcpHostContext.DefaultResourceUri, status["hostContext"]?["resourceUri"]?.ToString());
        Assert.Equal("streamable-http", response.HostContext?.Transport);
        Assert.Equal("prod", response.HostContext?.Instance);
    }

    [Fact]
    public async Task Read_only_configuration_surface_rejects_confirmed_persistence()
    {
        var client = Substitute.For<IHelpdeskAgentClient>();
        var context = new HelpdeskMcpHostContext(
            "prod",
            "streamable-http",
            "https://prod.example",
            HelpdeskMcpHostContext.DefaultResourceUri,
            McpOperationCatalog.Revision);
        var tools = new HelpdeskTools(client, new ReadOnlyHelpdeskMcpConfigurationSurface(AgentClientConfiguration.Empty), context);

        var response = await tools.helpdesk_config("set", Request("""{"key":"apiBaseUrl","value":"https://prod.example"}"""), confirm: true);

        Assert.Equal("validation_failed", response.Status);
        Assert.Contains("not available over streamable-http", response.Summary, StringComparison.Ordinal);
    }

    private static JsonElement Request(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
