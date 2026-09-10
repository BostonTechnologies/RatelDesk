using System.Security.Claims;

namespace HelpDesk.NewWeb.Services;

public interface IUserProvisioningService
{
    Task EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<Helpdesk.Shared.DTOs.Auth.CurrentUserAccessDto?> EnsureUserAccessAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
