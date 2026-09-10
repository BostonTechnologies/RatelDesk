namespace Helpdesk.Shared.Services;

public interface IAuthorizationScopeService
{
    bool CanViewIncident(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail);
    bool CanManageIncident(CurrentUserAccessProfile access, string? organizationId);
    bool CanViewRequest(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail);
    bool CanManageRequest(CurrentUserAccessProfile access, string? organizationId);
    bool CanViewChange(CurrentUserAccessProfile access, string? organizationId, string? customerId, string? requesterEmail);
    bool CanManageChange(CurrentUserAccessProfile access, string? organizationId);
}
