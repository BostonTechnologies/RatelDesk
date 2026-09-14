using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.Auth;

public sealed record BuiltInRoleDefinition(
    string Key,
    string Name,
    RoleScopeKind Scope,
    IReadOnlyList<string> Permissions,
    bool IsProtected = true);

public static class RoleDefinitionCatalog
{
    // Tenant administrators may compose operational roles for a tenant, but
    // cannot delegate tenant administration or instance-wide capabilities.
    public static readonly IReadOnlySet<string> TenantAdministratorPermissionCeiling =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            HelpdeskPermissions.SelfServiceUser,
            HelpdeskPermissions.IncidentUser,
            HelpdeskPermissions.IncidentManager,
            HelpdeskPermissions.IncidentRead,
            HelpdeskPermissions.IncidentWrite,
            HelpdeskPermissions.RequestUser,
            HelpdeskPermissions.RequestManager,
            HelpdeskPermissions.RequestRead,
            HelpdeskPermissions.RequestWrite,
            HelpdeskPermissions.ChangeUser,
            HelpdeskPermissions.ChangeManager,
            HelpdeskPermissions.ChangeRead,
            HelpdeskPermissions.ChangeWrite,
            HelpdeskPermissions.ChangeApprove
        };

    public static readonly IReadOnlyList<BuiltInRoleDefinition> BuiltIns =
    [
        new(
            "InstanceAdministrator",
            "Instance Administrator",
            RoleScopeKind.Instance,
            [HelpdeskPermissions.HelpdeskAdmin]),
        new(
            ScopedRoleCatalog.SelfServiceUser,
            "Self-service User",
            RoleScopeKind.OwnResource,
            HelpdeskPermissions.UserBundle),
        new(
            ScopedRoleCatalog.TenantAdministrator,
            "Tenant Administrator",
            RoleScopeKind.Tenant,
            [
                HelpdeskPermissions.TenantUsersManage,
                HelpdeskPermissions.TenantRolesAssign,
                HelpdeskPermissions.TenantSettingsManage
            ]),
        new(
            ScopedRoleCatalog.Technician,
            "Technician",
            RoleScopeKind.Tenant,
            HelpdeskPermissions.TechnicalBundle),
        new(ScopedRoleCatalog.IncidentReader, "Incident Reader", RoleScopeKind.Tenant, [HelpdeskPermissions.IncidentRead]),
        new(ScopedRoleCatalog.IncidentWriter, "Incident Writer", RoleScopeKind.Tenant, [HelpdeskPermissions.IncidentRead, HelpdeskPermissions.IncidentWrite]),
        new(ScopedRoleCatalog.RequestReader, "Request Reader", RoleScopeKind.Tenant, [HelpdeskPermissions.RequestRead]),
        new(ScopedRoleCatalog.RequestWriter, "Request Writer", RoleScopeKind.Tenant, [HelpdeskPermissions.RequestRead, HelpdeskPermissions.RequestWrite]),
        new(ScopedRoleCatalog.ChangeReader, "Change Reader", RoleScopeKind.Tenant, [HelpdeskPermissions.ChangeRead]),
        new(ScopedRoleCatalog.ChangeWriter, "Change Writer", RoleScopeKind.Tenant, [HelpdeskPermissions.ChangeRead, HelpdeskPermissions.ChangeWrite]),
        new(ScopedRoleCatalog.ChangeApprover, "Change Approver", RoleScopeKind.Tenant, [HelpdeskPermissions.ChangeRead, HelpdeskPermissions.ChangeApprove])
    ];

    public static BuiltInRoleDefinition? Find(string? key) =>
        BuiltIns.FirstOrDefault(definition =>
            string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsAssignablePermission(string permission) =>
        HelpdeskPermissions.AssignablePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public static bool IsWithinTenantAdministratorPermissionCeiling(IEnumerable<string> permissions) =>
        permissions.All(TenantAdministratorPermissionCeiling.Contains);

    public static IReadOnlyList<string> NormalizePermissions(IEnumerable<string>? permissions) =>
        (permissions ?? [])
        .Where(permission => !string.IsNullOrWhiteSpace(permission))
        .Select(permission => permission.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static bool SatisfiesDependencies(IReadOnlyCollection<string> permissions)
    {
        var grants = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (!grants.Contains(HelpdeskPermissions.IncidentManager) || grants.Contains(HelpdeskPermissions.IncidentUser)) &&
               (!grants.Contains(HelpdeskPermissions.IncidentWrite) || grants.Contains(HelpdeskPermissions.IncidentRead)) &&
               (!grants.Contains(HelpdeskPermissions.RequestManager) || grants.Contains(HelpdeskPermissions.RequestUser)) &&
               (!grants.Contains(HelpdeskPermissions.RequestWrite) || grants.Contains(HelpdeskPermissions.RequestRead)) &&
               (!grants.Contains(HelpdeskPermissions.ChangeManager) || grants.Contains(HelpdeskPermissions.ChangeUser)) &&
               (!grants.Contains(HelpdeskPermissions.ChangeWrite) || grants.Contains(HelpdeskPermissions.ChangeRead)) &&
               (!grants.Contains(HelpdeskPermissions.ChangeApprove) || grants.Contains(HelpdeskPermissions.ChangeRead));
    }
}
