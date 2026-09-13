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
    public bool UsesScopedPermissions { get; init; }

    public IReadOnlyList<ScopedPermissionGrant> ScopedPermissionGrants { get; init; } = [];
}
