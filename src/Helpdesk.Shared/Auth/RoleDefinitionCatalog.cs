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
            ScopedRoleCatalog.Technician,
            "Technician",
            RoleScopeKind.Tenant,
            HelpdeskPermissions.TechnicalBundle)
    ];

    public static BuiltInRoleDefinition? Find(string? key) =>
        BuiltIns.FirstOrDefault(definition =>
            string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsAssignablePermission(string permission) =>
        HelpdeskPermissions.AssignablePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

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
               (!grants.Contains(HelpdeskPermissions.RequestManager) || grants.Contains(HelpdeskPermissions.RequestUser)) &&
               (!grants.Contains(HelpdeskPermissions.ChangeManager) || grants.Contains(HelpdeskPermissions.ChangeUser));
    }
}
