using Helpdesk.Infrastructure.Auth.Rbac;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;

namespace Helpdesk.Tests.Infrastructure;

public class AuthorizationScopeServiceTests
{
    [Fact]
    public void Incident_user_can_only_view_own_ticket_in_allowed_tenant()
    {
        var access = Profile("org-1", "customer-1", HelpdeskPermissions.IncidentUser);
        var service = new AuthorizationScopeService();

        Assert.True(service.CanViewIncident(access, "org-1", "customer-1", null));
        Assert.False(service.CanViewIncident(access, "org-1", "customer-2", null));
        Assert.False(service.CanViewIncident(access, "org-2", "customer-1", null));
    }

    [Fact]
    public void Manager_can_view_all_tickets_in_allowed_tenant()
    {
        var access = Profile("org-1", "customer-1", HelpdeskPermissions.IncidentManager);
        var service = new AuthorizationScopeService();

        Assert.True(service.CanViewIncident(access, "org-1", "customer-2", null));
        Assert.False(service.CanViewIncident(access, "org-2", "customer-2", null));
    }

    [Fact]
    public void Matching_requester_email_without_matching_customer_id_does_not_grant_own_resource_access()
    {
        var access = Profile("org-1", "customer-1", HelpdeskPermissions.IncidentUser);
        var service = new AuthorizationScopeService();

        Assert.False(service.CanViewIncident(access, "org-1", "customer-2", "tester@example.com"));
        Assert.False(access.CanViewRequest("org-1", "customer-2", "tester@example.com"));
        Assert.False(access.CanViewChange("org-1", null, "tester@example.com"));
    }

    [Fact]
    public void Scoped_manager_grant_does_not_apply_to_another_allowed_tenant()
    {
        var access = Profile("org-a", "customer-1", HelpdeskPermissions.IncidentManager) with
        {
            ScopedPermissionGrants = new HashSet<ScopedPermissionGrant>
            {
                new(HelpdeskPermissions.IncidentManager, "org-a"),
                new(HelpdeskPermissions.IncidentUser, "org-b")
            }
        };
        var service = new AuthorizationScopeService();

        Assert.True(service.CanManageIncident(access, "org-a"));
        Assert.False(service.CanManageIncident(access, "org-b"));
    }

    [Fact]
    public void Scoped_permission_claims_preserve_their_organization_pairing()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Tester"),
                new System.Security.Claims.Claim("scoped_permission", $"{HelpdeskPermissions.IncidentManager}|org-a"),
                new System.Security.Claims.Claim("scoped_permission", $"{HelpdeskPermissions.IncidentUser}|org-b")
            ],
            "test"));

        var access = CurrentUserAccessProfile.FromClaims(principal);

        Assert.True(access.HasPermission(HelpdeskPermissions.IncidentManager, "org-a"));
        Assert.False(access.HasPermission(HelpdeskPermissions.IncidentManager, "org-b"));
        Assert.Equal(["org-a"], access.OrganizationIdsFor(HelpdeskPermissions.IncidentManager));
    }

    private static CurrentUserAccessProfile Profile(string orgId, string customerId, params string[] permissions) => new(
        true,
        "Tester",
        "tester@example.com",
        orgId,
        "Org",
        customerId,
        false,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        permissions.ToHashSet(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { orgId },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
