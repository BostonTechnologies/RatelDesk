using Helpdesk.Mcp.Http.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Xunit;

namespace Helpdesk.Tests.Mcp;

public sealed class RequiredMcpClaimsHandlerTests
{
    [Fact]
    public async Task Requires_every_configured_scope_and_group()
    {
        var requirement = new RequiredMcpClaimsRequirement(
            new HashSet<string>(["helpdesk.read", "helpdesk.write"], StringComparer.Ordinal),
            new HashSet<string>(["ai-assistant"], StringComparer.Ordinal));
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "helpdesk.read helpdesk.write"),
            new Claim("groups", "ai-assistant")
        ], "test");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new RequiredMcpClaimsHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Rejects_an_identity_missing_a_required_scope()
    {
        var requirement = new RequiredMcpClaimsRequirement(
            new HashSet<string>(["helpdesk.read", "helpdesk.write"], StringComparer.Ordinal),
            new HashSet<string>(["ai-assistant"], StringComparer.Ordinal));
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "helpdesk.read"),
            new Claim("groups", "ai-assistant")
        ], "test");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new RequiredMcpClaimsHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
