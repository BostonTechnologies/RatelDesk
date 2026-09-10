using System.Text.Json;
using Helpdesk.AgentClient;
using Xunit;

namespace Helpdesk.Tests.Mcp;

public sealed class AgentClientPolicyTests
{
    [Fact]
    public void Redaction_never_returns_app_password()
    {
        var config = new AgentClientConfiguration("https://api.example", "https://auth.example/token", "client", "agent", "super-secret", "scope", null);
        Assert.DoesNotContain("super-secret", JsonSerializer.Serialize(AgentClientConfigurationResolver.Redact(config)), StringComparison.Ordinal);
    }

    [Fact]
    public void Isolated_mcp_configuration_is_loaded_from_the_deployment_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"helpdesk-mcp-config-{Guid.NewGuid():N}.json");
        try
        {
            var expected = new AgentClientConfiguration("https://dev-api.example", "https://dev-auth.example/token", "dev-client", "dev-agent", "dev-password", "dev-scope", "dev-agent@example");
            new AgentClientConfigurationStore(path).Save(expected);

            var actual = AgentClientConfigurationResolver.LoadIsolated(path);

            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("assign")]
    [InlineData("connectivity_test")]
    public void Mutations_require_confirmation(string operation) => Assert.True(MutationPolicy.RequiresConfirmation(operation));

    [Theory]
    [InlineData("get")]
    [InlineData("list")]
    [InlineData("search")]
    public void Read_operations_do_not_require_confirmation(string operation) => Assert.False(MutationPolicy.RequiresConfirmation(operation));

    [Fact]
    public void Confirmation_contract_echoes_operation_and_ids()
    {
        var confirmation = MutationPolicy.Confirmation("assign_incident", "INC-12345", "user-123");
        Assert.Equal("confirm", confirmation.ConfirmField);
        Assert.Equal(["INC-12345", "user-123"], confirmation.AffectedIds);
    }
}
