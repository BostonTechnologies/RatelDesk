using System.Security.Claims;
using Helpdesk.API.Middleware;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;

namespace Helpdesk.Tests.Api;

public sealed class UserAccessClaimsMiddlewareTests
{
    [Fact]
    public async Task Application_access_claims_are_derived_from_the_resolved_profile()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "external-user"),
                    new Claim("organization_id", "attacker-org"),
                    new Claim("allowed_organization_id", "attacker-org"),
                    new Claim("customer_id", "attacker-customer"),
                    new Claim("scoped_permission", $"{HelpdeskPermissions.IncidentManager}|attacker-org")
                ],
                "Test"))
        };
        var profile = new CurrentUserAccessProfile(
            true,
            "External user",
            "external@example.test",
            "server-org",
            "Server organization",
            "server-customer",
            false,
            new HashSet<string>(),
            new HashSet<string> { HelpdeskPermissions.IncidentManager },
            new HashSet<string> { "server-org" },
            new HashSet<string>())
        {
            ScopedPermissionGrants = new HashSet<ScopedPermissionGrant>
            {
                new(HelpdeskPermissions.IncidentManager, "server-org")
            }
        };
        var accessService = new RecordingAccessService(profile);
        var middleware = new UserAccessClaimsMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessService);

        Assert.DoesNotContain(accessService.ObservedClaims, claim =>
            claim.Type is "organization_id" or "allowed_organization_id" or "customer_id" or "scoped_permission");
        Assert.Contains(context.User.Claims, claim => claim.Type == "organization_id" && claim.Value == "server-org");
        Assert.Contains(context.User.Claims, claim => claim.Type == "customer_id" && claim.Value == "server-customer");
        Assert.Contains(context.User.Claims, claim => claim.Type == "scoped_permission" &&
            claim.Value == $"{HelpdeskPermissions.IncidentManager}|server-org");
        Assert.DoesNotContain(context.User.Claims, claim => claim.Value.StartsWith("attacker-", StringComparison.Ordinal));
    }

    private sealed class RecordingAccessService(CurrentUserAccessProfile profile) : ICurrentUserAccessService
    {
        public IReadOnlyList<Claim> ObservedClaims { get; private set; } = [];

        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            ObservedClaims = user.Claims.ToArray();
            return Task.FromResult(profile);
        }
    }
}
