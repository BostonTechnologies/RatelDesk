extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Models;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class AuthentikAiAgentOptionsValidatorTests
{
    [Fact]
    public void Validate_Succeeds_When_Feature_Is_Disabled()
    {
        var validator = new AuthentikAiAgentOptionsValidator();

        var result = validator.Validate(null, new AuthentikAiAgentOptions
        {
            Enabled = false
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_When_Enabled_Without_Required_Values()
    {
        var validator = new AuthentikAiAgentOptionsValidator();

        var result = validator.Validate(null, new AuthentikAiAgentOptions
        {
            Enabled = true,
            CallbackPath = "signin-authentik-ai-agent"
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Authority", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("Audience", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("CallbackPath", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("RequiredGroups", StringComparison.Ordinal));
    }
}
