using Helpdesk.Mcp.Http.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Helpdesk.Tests.Mcp;

public sealed class AuthentikMcpOptionsValidatorTests
{
    [Fact]
    public void Accepts_a_canonical_https_resource_identity()
    {
        var validator = new AuthentikMcpOptionsValidator(Options.Create(new HelpdeskMcpHttpOptions
        {
            PublicResourceUri = "https://helpdesk-dev.example/mcp"
        }));

        var result = validator.Validate(null, new AuthentikMcpOptions
        {
            Authority = "https://auth.example",
            Audience = "https://helpdesk-dev.example/mcp",
            RequiredScopes = ["helpdesk.mcp"],
            RequiredGroups = ["ai-assistant"]
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rejects_an_audience_that_does_not_match_the_resource_identity()
    {
        var validator = new AuthentikMcpOptionsValidator(Options.Create(new HelpdeskMcpHttpOptions
        {
            PublicResourceUri = "https://helpdesk-prod.example/mcp"
        }));

        var result = validator.Validate(null, new AuthentikMcpOptions
        {
            Authority = "https://auth.example",
            Audience = "https://helpdesk-dev.example/mcp",
            RequiredScopes = ["helpdesk.mcp"],
            RequiredGroups = ["ai-assistant"]
        });

        Assert.False(result.Succeeded);
    }
}
