namespace Helpdesk.Shared.Auth;

public static class ScopedRoleCatalog
{
    public const string SelfServiceUser = "SelfServiceUser";
    public const string Technician = "Technician";
    public const string TenantAdministrator = "TenantAdministrator";
    public const string IncidentReader = "IncidentReader";
    public const string IncidentWriter = "IncidentWriter";
    public const string RequestReader = "RequestReader";
    public const string RequestWriter = "RequestWriter";
    public const string RequestExecutor = "RequestExecutor";
    public const string ChangeReader = "ChangeReader";
    public const string ChangeWriter = "ChangeWriter";
    public const string ChangeApprover = "ChangeApprover";

    public static bool IsSupported(string roleKey) => RoleDefinitionCatalog.Find(roleKey) is { Scope: not Models.RoleScopeKind.Instance };

    public static IReadOnlyList<string> PermissionsFor(string roleKey) =>
        RoleDefinitionCatalog.Find(roleKey)?.Permissions ?? [];
}
