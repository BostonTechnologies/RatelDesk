using System.Security.Claims;
using Helpdesk.Shared.DTOs.Auth;

namespace HelpDesk.NewWeb.Services;

/// <summary>Projects current API authorization into either kind of Web session.</summary>
public static class WebAccessClaimsProjection
{
    public static void Apply(ClaimsIdentity identity, CurrentUserAccessDto access)
    {
        RemoveAccessClaims(identity);
        if (access.UsesScopedPermissions) AddClaim(identity, "permission_scope_mode", "scoped");
        AddClaim(identity, "organization_id", access.PrimaryOrganizationId);
        AddClaim(identity, "customer_id", access.CustomerId);
        foreach (var organization in access.AllowedOrganizationIds) AddClaim(identity, "allowed_organization_id", organization);
        foreach (var organization in access.ManagedOrganizationIds) AddClaim(identity, "managed_organization_id", organization);
        foreach (var grant in access.ScopedPermissionGrants) AddClaim(identity, "scoped_permission", grant.ToString());
        foreach (var role in access.RoleBundles.Concat(access.Permissions))
        {
            AddClaim(identity, ClaimTypes.Role, role);
            AddClaim(identity, "roles", role);
        }
    }

    private static void RemoveAccessClaims(ClaimsIdentity identity)
    {
        foreach (var claim in identity.Claims.Where(claim => claim.Type is
                     "organization_id" or
                     "allowed_organization_id" or
                     "managed_organization_id" or
                     "customer_id" or
                     "scoped_permission" or
                     "permission_scope_mode" or
                     ClaimTypes.Role or
                     "roles").ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }

    private static void AddClaim(ClaimsIdentity identity, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(type, value))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

}
