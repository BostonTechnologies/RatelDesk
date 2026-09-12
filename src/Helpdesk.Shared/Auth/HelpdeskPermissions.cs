namespace Helpdesk.Shared.Auth;

public static class HelpdeskPermissions
{
    public const string SelfServiceUser = "SelfService.User";
    public const string IncidentUser = "Incident.User";
    public const string IncidentManager = "Incident.Manager";
    public const string RequestUser = "Request.User";
    public const string RequestManager = "Request.Manager";
    public const string ChangeUser = "Change.User";
    public const string ChangeManager = "Change.Manager";
    public const string DataManagementAdmin = "DataManagement.Admin";
    public const string HelpdeskAdmin = "HelpdeskAdmin";

    public static readonly string[] AssignablePermissions =
    [
        SelfServiceUser,
        IncidentUser,
        IncidentManager,
        RequestUser,
        RequestManager,
        ChangeUser,
        ChangeManager,
        DataManagementAdmin
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
        RequestUser,
        RequestManager,
        ChangeUser,
        ChangeManager
    ];
}
