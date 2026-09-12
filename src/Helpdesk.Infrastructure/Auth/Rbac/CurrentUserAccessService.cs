using System.Security.Claims;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Auth.Rbac;

public sealed class CurrentUserAccessService(HelpdeskDbContext db) : ICurrentUserAccessService
{
    public async Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Empty(false);
        }

        var groups = ClaimValues(user, "groups", ClaimTypes.Role, "roles").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var isAdmin = IsAdmin(user, groups);

        if (isAdmin)
        {
            permissions.Add(HelpdeskPermissions.HelpdeskAdmin);
            bundles.Add(HelpdeskRoleBundles.HelpdeskAdmin);
        }

        AddDirectPermissionClaims(groups, permissions);

        if (groups.Contains(AuthentikRbacGroups.ClientAdmin) || groups.Contains(HelpdeskPermissions.DataManagementAdmin))
        {
            bundles.Add(HelpdeskRoleBundles.DataManagementAdmin);
            permissions.Add(HelpdeskPermissions.DataManagementAdmin);
        }

        if (groups.Contains(AuthentikRbacGroups.Technical))
        {
            bundles.Add(HelpdeskRoleBundles.Technical);
            foreach (var permission in HelpdeskPermissions.TechnicalBundle)
            {
                permissions.Add(permission);
            }
        }

        var email = FirstClaim(user, ClaimTypes.Email, "email", "preferred_username");
        var issuer = FirstClaim(user, "iss")?.TrimEnd('/');
        var subject = FirstClaim(user, "sub");
        var authentikUserId = FirstClaim(user, "authentik_user_id", "ak_user_id");

        var link = await FindCustomerAuthLinkAsync(issuer, subject, authentikUserId, ct);
        Customer? customer = null;
        Organization? organization = null;
        if (link is not null)
        {
            customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == link.CustomerId, ct);
            if (customer?.IsEnabled == true)
            {
                organization = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customer.OrganizationId, ct);
            }
        }

        var hasActiveCustomer = customer?.IsEnabled == true && organization?.IsEnabled == true;
        var localAccountId = IsLocalAccount(user)
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var localDomainUser = string.IsNullOrWhiteSpace(localAccountId)
            ? null
            : await db.Users.AsNoTracking().FirstOrDefaultAsync(domainUser => domainUser.Id == localAccountId, ct);
        var localOrganization = localDomainUser is null || string.IsNullOrWhiteSpace(localDomainUser.OrganizationId)
            ? null
            : await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == localDomainUser.OrganizationId, ct);
        var hasActiveLocalDomainUser = localDomainUser is not null && localOrganization?.IsEnabled == true;

        if (hasActiveCustomer)
        {
            bundles.Add(HelpdeskRoleBundles.User);
            foreach (var permission in HelpdeskPermissions.UserBundle)
            {
                permissions.Add(permission);
            }

            if (groups.Contains(AuthentikRbacGroups.Technical))
            {
                bundles.Add(HelpdeskRoleBundles.Technical);
                foreach (var permission in HelpdeskPermissions.TechnicalBundle)
                {
                    permissions.Add(permission);
                }
            }
        }
        else if (hasActiveLocalDomainUser)
        {
            AddLocalRoleBundle(localDomainUser!.Role, bundles, permissions);
        }

        var primaryOrganizationId = hasActiveCustomer
            ? customer!.OrganizationId
            : hasActiveLocalDomainUser
                ? localDomainUser!.OrganizationId
                : null;
        var allowedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var managedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(primaryOrganizationId))
        {
            allowedOrganizations.Add(primaryOrganizationId);
        }

        var isTechnical = bundles.Contains(HelpdeskRoleBundles.Technical);
        if (isTechnical && !string.IsNullOrWhiteSpace(primaryOrganizationId))
        {
            var managed = await db.Organizations.AsNoTracking()
                .Where(x => x.State == Helpdesk.Shared.Models.EntityState.Enabled && x.ItSupportOrganizationId == primaryOrganizationId)
                .Select(x => x.Id)
                .ToListAsync(ct);

            foreach (var organizationId in managed)
            {
                allowedOrganizations.Add(organizationId);
                managedOrganizations.Add(organizationId);
            }
        }

        return new CurrentUserAccessProfile(
            IsAuthenticated: true,
            Name: user.Identity?.Name ?? FirstClaim(user, "name", "preferred_username") ?? email,
            Email: email,
            PrimaryOrganizationId: primaryOrganizationId,
            PrimaryOrganizationName: organization?.Name ?? localOrganization?.Name,
            CustomerId: hasActiveCustomer ? customer!.Id : null,
            IsHelpdeskAdmin: isAdmin,
            RoleBundles: bundles,
            Permissions: permissions,
            AllowedOrganizationIds: allowedOrganizations,
            ManagedOrganizationIds: managedOrganizations);
    }

    private async Task<CustomerAuthLink?> FindCustomerAuthLinkAsync(
        string? issuer,
        string? subject,
        string? authentikUserId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(subject))
        {
            var link = await db.CustomerAuthLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OidcIssuer == issuer && x.OidcSubject == subject, ct);
            if (link is not null) return link;
        }

        if (!string.IsNullOrWhiteSpace(authentikUserId))
        {
            var link = await db.CustomerAuthLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AuthentikUserId == authentikUserId, ct);
            if (link is not null) return link;
        }

        return null;
    }

    private static bool IsAdmin(ClaimsPrincipal user, HashSet<string> groups) =>
        user.IsInRole(HelpdeskPermissions.HelpdeskAdmin) ||
        groups.Contains(HelpdeskPermissions.HelpdeskAdmin) ||
        groups.Contains(AuthentikRbacGroups.HelpdeskAdmin);

    private static bool IsLocalAccount(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue("auth_mode"), "local", StringComparison.OrdinalIgnoreCase);

    private static void AddLocalRoleBundle(
        string role,
        HashSet<string> bundles,
        HashSet<string> permissions)
    {
        var bundle = role switch
        {
            "User" => HelpdeskRoleBundles.User,
            "Technician" => HelpdeskRoleBundles.Technical,
            _ => null
        };
        if (bundle is null)
        {
            return;
        }

        bundles.Add(bundle);
        foreach (var permission in bundle == HelpdeskRoleBundles.User
                     ? HelpdeskPermissions.UserBundle
                     : HelpdeskPermissions.TechnicalBundle)
        {
            permissions.Add(permission);
        }
    }

    private static void AddDirectPermissionClaims(HashSet<string> groups, HashSet<string> permissions)
    {
        foreach (var permission in new[]
                 {
                     HelpdeskPermissions.SelfServiceUser,
                     HelpdeskPermissions.IncidentUser,
                     HelpdeskPermissions.IncidentManager,
                     HelpdeskPermissions.RequestUser,
                     HelpdeskPermissions.RequestManager,
                     HelpdeskPermissions.ChangeUser,
                     HelpdeskPermissions.ChangeManager,
                     HelpdeskPermissions.DataManagementAdmin
                 })
        {
            if (groups.Contains(permission))
            {
                permissions.Add(permission);
            }
        }
    }

    private static IEnumerable<string> ClaimValues(ClaimsPrincipal user, params string[] claimTypes) =>
        claimTypes.SelectMany(type => user.FindAll(type))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value));

    private static string? FirstClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
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
}
