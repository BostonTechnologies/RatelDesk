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
    public bool HasPermission(string permission) =>
        IsHelpdeskAdmin || Permissions.Contains(permission);

    public bool CanViewIncident(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageIncident(organizationId) || CanOwn(HelpdeskPermissions.IncidentUser, organizationId, customerId, requesterEmail);

    public bool CanManageIncident(string? organizationId) =>
        IsHelpdeskAdmin || InAllowedOrg(organizationId) && HasPermission(HelpdeskPermissions.IncidentManager);

    public bool CanViewRequest(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageRequest(organizationId) || CanOwn(HelpdeskPermissions.RequestUser, organizationId, customerId, requesterEmail);

    public bool CanManageRequest(string? organizationId) =>
        IsHelpdeskAdmin || InAllowedOrg(organizationId) && HasPermission(HelpdeskPermissions.RequestManager);

    public bool CanViewChange(string? organizationId, string? customerId, string? requesterEmail) =>
        CanManageChange(organizationId) || CanOwn(HelpdeskPermissions.ChangeUser, organizationId, customerId, requesterEmail);

    public bool CanManageChange(string? organizationId) =>
        IsHelpdeskAdmin || InAllowedOrg(organizationId) && HasPermission(HelpdeskPermissions.ChangeManager);

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
            user.FindAll("allowed_organization_id")
                .Select(claim => claim.Value)
                .Append(FirstClaim(user, "organization_id", "tenant_id"))
                .OfType<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            user.FindAll("managed_organization_id")
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
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
        InAllowedOrg(organizationId) &&
        HasPermission(permission) &&
        ((!string.IsNullOrWhiteSpace(customerId) && string.Equals(customerId, CustomerId, StringComparison.OrdinalIgnoreCase)) ||
         (!string.IsNullOrWhiteSpace(requesterEmail) && string.Equals(requesterEmail, Email, StringComparison.OrdinalIgnoreCase)));

    private bool InAllowedOrg(string? organizationId) =>
        !string.IsNullOrWhiteSpace(organizationId) && AllowedOrganizationIds.Contains(organizationId);
}
