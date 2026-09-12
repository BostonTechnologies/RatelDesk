using System.Security.Claims;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;

namespace Helpdesk.API.Middleware;

public sealed class UserAccessClaimsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserAccessService accessService)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.Identity is ClaimsIdentity identity)
        {
            RemoveScopedAccessClaims(identity);
            var access = await accessService.ResolveAsync(context.User, context.RequestAborted);
            RemoveApplicationAccessClaims(identity);
            AddClaim(identity, "organization_id", access.PrimaryOrganizationId);
            AddClaim(identity, "customer_id", access.CustomerId);
            foreach (var organizationId in access.AllowedOrganizationIds)
            {
                AddClaim(identity, "allowed_organization_id", organizationId);
            }
            foreach (var grant in access.ScopedPermissionGrants)
            {
                AddClaim(identity, "scoped_permission", grant.ToString());
            }
            foreach (var role in access.RoleBundles.Concat(access.Permissions))
            {
                AddClaim(identity, ClaimTypes.Role, role);
                AddClaim(identity, "roles", role);
            }
        }

        await next(context);
    }

    private static void RemoveApplicationAccessClaims(ClaimsIdentity identity)
    {
        RemoveScopedAccessClaims(identity);

        foreach (var claim in identity.Claims.Where(claim =>
                     claim.Type is ClaimTypes.Role or "roles" &&
                     IsApplicationRoleValue(claim.Value)).ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }

    private static void RemoveScopedAccessClaims(ClaimsIdentity identity)
    {
        foreach (var claim in identity.Claims.Where(claim => claim.Type is
                     "organization_id" or
                     "allowed_organization_id" or
                     "customer_id" or
                     "scoped_permission" or
                     "provider_role").ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }

    private static void AddClaim(ClaimsIdentity identity, string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (identity.HasClaim(type, value)) return;
        identity.AddClaim(new Claim(type, value));
    }

    private static bool IsApplicationRoleValue(string value) =>
        value is HelpdeskRoleBundles.User or
            HelpdeskRoleBundles.Technical or
            HelpdeskRoleBundles.DataManagementAdmin or
            HelpdeskRoleBundles.HelpdeskAdmin or
            HelpdeskPermissions.HelpdeskAdmin or
            HelpdeskPermissions.SelfServiceUser or
            HelpdeskPermissions.IncidentUser or
            HelpdeskPermissions.IncidentManager or
            HelpdeskPermissions.RequestUser or
            HelpdeskPermissions.RequestManager or
            HelpdeskPermissions.ChangeUser or
            HelpdeskPermissions.ChangeManager or
            HelpdeskPermissions.DataManagementAdmin or
            HelpdeskPermissions.TenantUsersManage or
            HelpdeskPermissions.TenantRolesAssign or
            HelpdeskPermissions.TenantSettingsManage;
}
