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
    public const string ChangeReader = "ChangeReader";
    public const string ChangeWriter = "ChangeWriter";
    public const string ChangeApprover = "ChangeApprover";

    public static bool IsSupported(string roleKey) =>
        roleKey is SelfServiceUser or Technician or TenantAdministrator or
            IncidentReader or IncidentWriter or RequestReader or RequestWriter or
            ChangeReader or ChangeWriter or ChangeApprover;

    public static IReadOnlyList<string> PermissionsFor(string roleKey) => roleKey switch
    {
        SelfServiceUser => HelpdeskPermissions.UserBundle,
        Technician => HelpdeskPermissions.TechnicalBundle,
        IncidentReader => [HelpdeskPermissions.IncidentRead],
        IncidentWriter => [HelpdeskPermissions.IncidentRead, HelpdeskPermissions.IncidentWrite],
        RequestReader => [HelpdeskPermissions.RequestRead],
        RequestWriter => [HelpdeskPermissions.RequestRead, HelpdeskPermissions.RequestWrite],
        ChangeReader => [HelpdeskPermissions.ChangeRead],
        ChangeWriter => [HelpdeskPermissions.ChangeRead, HelpdeskPermissions.ChangeWrite],
        ChangeApprover => [HelpdeskPermissions.ChangeRead, HelpdeskPermissions.ChangeApprove],
        TenantAdministrator =>
        [
            HelpdeskPermissions.TenantUsersManage,
            HelpdeskPermissions.TenantRolesAssign,
            HelpdeskPermissions.TenantSettingsManage
        ],
        _ => []
    };
}
