using Helpdesk.Shared.Services;

namespace Helpdesk.Shared.DTOs.Auth;

public sealed record CurrentUserAccessDto(
    bool IsAuthenticated,
    string? Name,
    string? Email,
    string? PrimaryOrganizationId,
    string? PrimaryOrganizationName,
    string? CustomerId,
    bool IsHelpdeskAdmin,
    IReadOnlyList<string> RoleBundles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> AllowedOrganizationIds,
    IReadOnlyList<string> ManagedOrganizationIds)
{
    /// <summary>Stable application identity of the authenticated caller, when one is linked.</summary>
    public string? UserId { get; init; }

    public bool UsesScopedPermissions { get; init; }

    public IReadOnlyList<ScopedPermissionGrant> ScopedPermissionGrants { get; init; } = [];
}
