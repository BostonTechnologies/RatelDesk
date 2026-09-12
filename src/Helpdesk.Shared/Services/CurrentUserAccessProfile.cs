using System.Security.Claims;
using Helpdesk.Shared.Auth;

namespace Helpdesk.Shared.Services;

public sealed record CurrentUserAccessProfile(
    bool IsAuthenticated,
    string? Name,
    string? Email,
    string? PrimaryOrganizationId,
    string? PrimaryOrganizationName,
    string? CustomerId,
    bool IsHelpdeskAdmin,
    IReadOnlySet<string> RoleBundles,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> AllowedOrganizationIds,
    IReadOnlySet<string> ManagedOrganizationIds)
{
    public IReadOnlySet<ScopedPermissionGrant> ScopedPermissionGrants { get; init; } =
        new HashSet<ScopedPermissionGrant>();

    public bool HasPermission(string permission) =>
        IsHelpdeskAdmin || Permissions.Contains(permission);

    public bool HasPermission(string permission, string? organizationId)
    {
        if (IsHelpdeskAdmin)
        {
            return true;
        }

        if (ScopedPermissionGrants.Count > 0)
        {
            return !string.IsNullOrWhiteSpace(organizationId) &&
                   ScopedPermissionGrants.Contains(new ScopedPermissionGrant(permission, organizationId));
        }

        return InAllowedOrg(organizationId) && Permissions.Contains(permission);
    }

    public IReadOnlySet<string> OrganizationIdsFor(string permission) =>
        ScopedPermissionGrants.Count > 0
            ? ScopedPermissionGrants
                .Where(grant => string.Equals(grant.Permission, permission, StringComparison.OrdinalIgnoreCase))
                .Select(grant => grant.OrganizationId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : HasPermission(permission)
                ? AllowedOrganizationIds
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool CanViewIncident(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageIncident(organizationId) || CanOwn(HelpdeskPermissions.IncidentUser, organizationId, customerId, requesterEmail);

    public bool CanManageIncident(string? organizationId) =>
        HasPermission(HelpdeskPermissions.IncidentManager, organizationId);

    public bool CanViewRequest(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageRequest(organizationId) || CanOwn(HelpdeskPermissions.RequestUser, organizationId, customerId, requesterEmail);

    public bool CanManageRequest(string? organizationId) =>
        HasPermission(HelpdeskPermissions.RequestManager, organizationId);

    public bool CanViewChange(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageChange(organizationId) || CanOwn(HelpdeskPermissions.ChangeUser, organizationId, customerId, requesterEmail);

    public bool CanManageChange(string? organizationId) =>
        HasPermission(HelpdeskPermissions.ChangeManager, organizationId);

    public static CurrentUserAccessProfile FromClaims(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Empty(false);
        }

        var roleValues = user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("roles"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAdmin =
            user.IsInRole(HelpdeskPermissions.HelpdeskAdmin) ||
            roleValues.Contains(HelpdeskPermissions.HelpdeskAdmin) ||
            roleValues.Contains(AuthentikRbacGroups.HelpdeskAdmin);

        var scopedPermissionGrants = user.FindAll("scoped_permission")
            .Select(claim => ScopedPermissionGrant.TryParse(claim.Value))
            .OfType<ScopedPermissionGrant>()
            .ToHashSet();

        var allowedOrganizationIds = user.FindAll("allowed_organization_id")
            .Select(claim => claim.Value)
            .Append(FirstClaim(user, "organization_id", "tenant_id"))
            .OfType<string>()
            .Concat(scopedPermissionGrants.Select(grant => grant.OrganizationId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CurrentUserAccessProfile(
            true,
            user.Identity?.Name ?? FirstClaim(user, "name", "preferred_username", ClaimTypes.Email, "email"),
            FirstClaim(user, ClaimTypes.Email, "email", "preferred_username"),
            FirstClaim(user, "organization_id", "tenant_id"),
            null,
            FirstClaim(user, "customer_id"),
            isAdmin,
            roleValues,
            roleValues,
            allowedOrganizationIds,
            user.FindAll("managed_organization_id")
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase))
        {
            ScopedPermissionGrants = scopedPermissionGrants
        };
    }

    private static string? FirstClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static CurrentUserAccessProfile Empty(bool authenticated) => new(
        authenticated,
        null,
        null,
        null,
        null,
        null,
        false,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private bool CanOwn(string permission, string? organizationId, string? customerId, string? requesterEmail) =>
        HasPermission(permission, organizationId) &&
        !string.IsNullOrWhiteSpace(customerId) &&
        string.Equals(customerId, CustomerId, StringComparison.OrdinalIgnoreCase);

    private bool InAllowedOrg(string? organizationId) =>
        !string.IsNullOrWhiteSpace(organizationId) && AllowedOrganizationIds.Contains(organizationId);
}

public sealed record ScopedPermissionGrant(string Permission, string OrganizationId)
{
    public override string ToString() => $"{Permission}|{OrganizationId}";

    public static ScopedPermissionGrant? TryParse(string value)
    {
        var separator = value.IndexOf('|', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return null;
        }

        var permission = value[..separator].Trim();
        var organizationId = value[(separator + 1)..].Trim();
        return string.IsNullOrWhiteSpace(permission) || string.IsNullOrWhiteSpace(organizationId)
            ? null
            : new ScopedPermissionGrant(permission, organizationId);
    }
}
