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
