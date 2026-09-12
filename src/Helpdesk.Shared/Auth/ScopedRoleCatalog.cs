namespace Helpdesk.Shared.Auth;

public static class ScopedRoleCatalog
{
    public const string SelfServiceUser = "SelfServiceUser";
    public const string Technician = "Technician";

    public static IReadOnlyList<string> PermissionsFor(string roleKey) => roleKey switch
    {
        SelfServiceUser => HelpdeskPermissions.UserBundle,
        Technician => HelpdeskPermissions.TechnicalBundle,
        _ => []
    };
}
