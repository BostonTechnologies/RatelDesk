using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;

namespace Helpdesk.Infrastructure.Auth.Rbac;

public sealed class AuthorizationScopeService : IAuthorizationScopeService
{
    public bool CanViewIncident(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageIncident(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.IncidentUser, organizationId, customerId, requesterEmail);

    public bool CanManageIncident(CurrentUserAccessProfile access, string? organizationId) =>
        access.CanManageIncident(organizationId);

    public bool CanViewRequest(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageRequest(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.RequestUser, organizationId, customerId, requesterEmail);

    public bool CanManageRequest(CurrentUserAccessProfile access, string? organizationId) =>
        access.CanManageRequest(organizationId);

    public bool CanViewChange(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageChange(access, organizationId) ||
        CanOwn(access, HelpdeskPermissions.ChangeUser, organizationId, customerId, requesterEmail);

    public bool CanManageChange(CurrentUserAccessProfile access, string? organizationId) =>
        access.CanManageChange(organizationId);

    private static bool CanOwn(CurrentUserAccessProfile access, string permission, string? organizationId, string? customerId, string? requesterEmail) =>
        access.HasPermission(permission, organizationId) &&
        ((!string.IsNullOrWhiteSpace(customerId) && string.Equals(customerId, access.CustomerId, StringComparison.OrdinalIgnoreCase)) ||
         (!string.IsNullOrWhiteSpace(requesterEmail) && string.Equals(requesterEmail, access.Email, StringComparison.OrdinalIgnoreCase)));

}
