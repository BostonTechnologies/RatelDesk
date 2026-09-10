using Hangfire.Dashboard;
using System.Security.Claims;

namespace Helpdesk.API.Background;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("HelpdeskAdmin"))
        {
            return true;
        }

        var roleClaim = user.Claims.Any(c =>
            (c.Type == "roles" || c.Type == ClaimTypes.Role) &&
            string.Equals(c.Value, "HelpdeskAdmin", StringComparison.OrdinalIgnoreCase));
        if (roleClaim)
        {
            return true;
        }

        return false;
    }
}
