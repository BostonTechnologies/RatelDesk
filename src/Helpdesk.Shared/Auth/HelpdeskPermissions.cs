namespace Helpdesk.Shared.Auth;

public static class HelpdeskPermissions
{
    public const string SelfServiceUser = "SelfService.User";
    public const string IncidentUser = "Incident.User";
    public const string IncidentManager = "Incident.Manager";
    public const string IncidentRead = "Incidents.Read";
    public const string IncidentWrite = "Incidents.Write";
    public const string RequestUser = "Request.User";
    public const string RequestManager = "Request.Manager";
    public const string RequestRead = "Requests.Read";
    public const string RequestWrite = "Requests.Write";
    public const string ChangeUser = "Change.User";
    public const string ChangeManager = "Change.Manager";
    public const string ChangeRead = "Changes.Read";
    public const string ChangeWrite = "Changes.Write";
    public const string ChangeApprove = "Changes.Approve";
    public const string DataManagementAdmin = "DataManagement.Admin";
    public const string HelpdeskAdmin = "HelpdeskAdmin";
    public const string TenantUsersManage = "Tenant.Users.Manage";
    public const string TenantRolesAssign = "Tenant.Roles.Assign";
    public const string TenantSettingsManage = "Tenant.Settings.Manage";

    public static readonly string[] AssignablePermissions =
    [
        SelfServiceUser,
        IncidentUser,
        IncidentManager,
        IncidentRead,
        IncidentWrite,
        RequestUser,
        RequestManager,
        RequestRead,
        RequestWrite,
        ChangeUser,
        ChangeManager,
        ChangeRead,
        ChangeWrite,
        ChangeApprove,
        DataManagementAdmin,
        TenantUsersManage,
        TenantRolesAssign,
        TenantSettingsManage
    ];

    public static readonly string[] UserBundle =
    [
        SelfServiceUser,
        IncidentUser,
        RequestUser
    ];

    public static readonly string[] TechnicalBundle =
    [
        SelfServiceUser,
        IncidentUser,
        IncidentManager,
        IncidentRead,
        IncidentWrite,
        RequestUser,
        RequestManager,
        RequestRead,
        RequestWrite,
        ChangeUser,
        ChangeManager,
        ChangeRead,
        ChangeWrite
    ];
}
