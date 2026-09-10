using System.Security.Claims;

namespace Helpdesk.Shared.Services;

public interface ICurrentUserAccessService
{
    Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default);
}
