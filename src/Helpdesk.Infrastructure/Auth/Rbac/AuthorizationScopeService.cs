using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;

namespace Helpdesk.Infrastructure.Auth.Rbac;

public sealed class AuthorizationScopeService : IAuthorizationScopeService
{
    public bool CanViewIncident(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageIncident(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.IncidentUser, organizationId, customerId, requesterEmail);

    public bool CanManageIncident(CurrentUserAccessProfile access, string? organizationId) =>
        access.IsHelpdeskAdmin || InAllowedOrg(access, organizationId) && access.HasPermission(HelpdeskPermissions.IncidentManager);

    public bool CanViewRequest(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageRequest(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.RequestUser, organizationId, customerId, requesterEmail);

    public bool CanManageRequest(CurrentUserAccessProfile access, string? organizationId) =>
        access.IsHelpdeskAdmin || InAllowedOrg(access, organizationId) && access.HasPermission(HelpdeskPermissions.RequestManager);

    public bool CanViewChange(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageChange(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.ChangeUser, organizationId, customerId, requesterEmail);

    public bool CanManageChange(CurrentUserAccessProfile access, string? organizationId) =>
        access.IsHelpdeskAdmin || InAllowedOrg(access, organizationId) && access.HasPermission(HelpdeskPermissions.ChangeManager);

    private static bool CanOwn(CurrentUserAccessProfile access, string permission, string? organizationId, string? customerId, string? requesterEmail) =>
        InAllowedOrg(access, organizationId) &&
        access.HasPermission(permission) &&
        ((!string.IsNullOrWhiteSpace(customerId) && string.Equals(customerId, access.CustomerId, StringComparison.OrdinalIgnoreCase)) ||
         (!string.IsNullOrWhiteSpace(requesterEmail) && string.Equals(requesterEmail, access.Email, StringComparison.OrdinalIgnoreCase)));

    private static bool InAllowedOrg(CurrentUserAccessProfile access, string? organizationId) =>
        !string.IsNullOrWhiteSpace(organizationId) && access.AllowedOrganizationIds.Contains(organizationId);
}
