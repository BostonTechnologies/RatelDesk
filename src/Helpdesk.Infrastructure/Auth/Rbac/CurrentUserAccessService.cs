using System.Security.Claims;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Auth.Rbac;

public sealed class CurrentUserAccessService : ICurrentUserAccessService
{
    private readonly HelpdeskDbContext _db;
    private readonly RatelDeskIdentityDbContext? _identityDb;

    public CurrentUserAccessService(HelpdeskDbContext db)
        : this(db, null)
    {
    }

    public CurrentUserAccessService(HelpdeskDbContext db, RatelDeskIdentityDbContext? identityDb)
    {
        _db = db;
        _identityDb = identityDb;
    }

    public async Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Empty(false);
        }

        var localAccountId = IsLocalAccount(user)
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (_identityDb is not null && !string.IsNullOrWhiteSpace(localAccountId))
        {
            var isEnabled = await _identityDb.Users.AsNoTracking()
                .AnyAsync(account => account.Id == localAccountId && account.IsEnabled, ct);
            if (!isEnabled)
            {
                return Empty(true);
            }
        }

        var groups = ClaimValues(user, "groups", "provider_role").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issuer = FirstClaim(user, "iss")?.TrimEnd('/');
        if (IsApplicationRoleClaimSource(user) || string.IsNullOrWhiteSpace(issuer))
        {
            groups.UnionWith(ClaimValues(user, ClaimTypes.Role, "roles"));
        }
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scopedPermissionGrants = new HashSet<ScopedPermissionGrant>();
        var isAdmin = groups.Contains(HelpdeskPermissions.HelpdeskAdmin) ||
                      groups.Contains(AuthentikRbacGroups.HelpdeskAdmin);

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
        var subject = FirstClaim(user, "sub");
        var authentikUserId = FirstClaim(user, "authentik_user_id", "ak_user_id");

        var link = await FindCustomerAuthLinkAsync(localAccountId, issuer, subject, authentikUserId, ct);
        Customer? customer = null;
        Organization? organization = null;
        if (link is not null)
        {
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == link.CustomerId, ct);
            if (customer?.IsEnabled == true)
            {
                organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customer.OrganizationId, ct);
            }
        }

        var hasActiveCustomer = customer?.IsEnabled == true && organization?.IsEnabled == true;
        var domainUserId = localAccountId ?? link?.DomainUserId;
        var domainUser = string.IsNullOrWhiteSpace(domainUserId)
            ? null
            : await _db.Users.AsNoTracking().FirstOrDefaultAsync(domainUser => domainUser.Id == domainUserId, ct);
        var domainUserOrganization = domainUser is null || string.IsNullOrWhiteSpace(domainUser.OrganizationId)
            ? null
            : await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == domainUser.OrganizationId, ct);
        var hasActiveDomainUser = domainUser is not null && domainUserOrganization?.IsEnabled == true;
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
        if (hasActiveDomainUser)
        {
            var assignments = await (
                    from assignment in _db.ScopedRoleAssignments.AsNoTracking()
                    join assignmentOrganization in _db.Organizations.AsNoTracking()
                        on assignment.OrganizationId equals assignmentOrganization.Id
                    where assignment.UserId == domainUser!.Id && assignmentOrganization.IsEnabled
                    select assignment)
                .ToListAsync(ct);

            var assignedRoleKeys = assignments
                .Select(assignment => assignment.RoleKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var rolePermissions = await _db.Roles.AsNoTracking()
                .Where(role => assignedRoleKeys.Contains(role.Key) && role.Scope != RoleScopeKind.Instance)
                .Select(role => new
                {
                    role.Key,
                    role.OwnerOrganizationId,
                    Permissions = role.Permissions.Select(permission => permission.Permission).ToArray()
                })
                .ToDictionaryAsync(role => role.Key, StringComparer.OrdinalIgnoreCase, ct);
            foreach (var assignment in assignments)
            {
                if (rolePermissions.TryGetValue(assignment.RoleKey, out var persistedRole) &&
                    persistedRole.OwnerOrganizationId is not null &&
                    !string.Equals(persistedRole.OwnerOrganizationId, assignment.OrganizationId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var assignedPermissions = persistedRole is not null
                    ? (IReadOnlyList<string>)persistedRole.Permissions
                    : ScopedRoleCatalog.PermissionsFor(assignment.RoleKey);
                foreach (var permission in assignedPermissions)
                {
                    permissions.Add(permission);
                    scopedPermissionGrants.Add(new ScopedPermissionGrant(permission, assignment.OrganizationId));
                }
            }

            if (assignments.Count == 0)
            {
                AddLocalRoleBundle(domainUser.Role, bundles, permissions);
            }
        }

        var primaryOrganizationId = hasActiveCustomer
            ? customer!.OrganizationId
            : hasActiveDomainUser
                ? domainUser!.OrganizationId
                : null;
        var allowedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var managedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(primaryOrganizationId))
        {
            allowedOrganizations.Add(primaryOrganizationId);
        }
        foreach (var grant in scopedPermissionGrants)
        {
            allowedOrganizations.Add(grant.OrganizationId);
        }

        var isTechnical = bundles.Contains(HelpdeskRoleBundles.Technical);
        if (isTechnical && !string.IsNullOrWhiteSpace(primaryOrganizationId))
        {
            var managed = await _db.Organizations.AsNoTracking()
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
            PrimaryOrganizationName: organization?.Name ?? domainUserOrganization?.Name,
            CustomerId: hasActiveCustomer ? customer!.Id : null,
            IsHelpdeskAdmin: isAdmin,
            RoleBundles: bundles,
            Permissions: permissions,
            AllowedOrganizationIds: allowedOrganizations,
            ManagedOrganizationIds: managedOrganizations)
        {
            ScopedPermissionGrants = scopedPermissionGrants
        };
    }

    private async Task<CustomerAuthLink?> FindCustomerAuthLinkAsync(
        string? localAccountId,
        string? issuer,
        string? subject,
        string? authentikUserId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(localAccountId))
        {
            var link = await _db.CustomerAuthLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LocalAccountId == localAccountId, ct);
            if (link is not null) return link;
        }

        if (!string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(subject))
        {
            var link = await _db.CustomerAuthLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OidcIssuer == issuer && x.OidcSubject == subject, ct);
            if (link is not null) return link;
        }

        if (!string.IsNullOrWhiteSpace(authentikUserId))
        {
            var link = await _db.CustomerAuthLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AuthentikUserId == authentikUserId, ct);
            if (link is not null) return link;
        }

        return null;
    }

    private static bool IsLocalAccount(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue("auth_mode"), "local", StringComparison.OrdinalIgnoreCase);

    private static bool IsApplicationRoleClaimSource(ClaimsPrincipal user) =>
        IsLocalAccount(user) ||
        string.Equals(user.FindFirstValue("auth_mode"), "ai_agent", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(user.FindFirstValue("auth_mode"), "system", StringComparison.OrdinalIgnoreCase);

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
