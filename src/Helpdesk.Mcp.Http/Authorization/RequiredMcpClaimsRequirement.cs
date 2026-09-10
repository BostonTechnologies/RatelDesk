using Microsoft.AspNetCore.Authorization;
using Helpdesk.Mcp.Http.Configuration;

namespace Helpdesk.Mcp.Http.Authorization;

public sealed record RequiredMcpClaimsRequirement(IReadOnlySet<string> Scopes, IReadOnlySet<string> Groups) : IAuthorizationRequirement;

public sealed class RequiredMcpClaimsHandler : AuthorizationHandler<RequiredMcpClaimsRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequiredMcpClaimsRequirement requirement)
    {
        var scopes = context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        var groups = context.User.FindAll("groups").Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        if (requirement.Scopes.IsSubsetOf(scopes) && requirement.Groups.IsSubsetOf(groups)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
