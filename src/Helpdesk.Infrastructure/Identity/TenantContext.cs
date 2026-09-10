using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Helpdesk.Infrastructure.Identity;

public class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? TenantId => GetFirstClaimValue("organization_id", "tid", "tenant_id");

    public string? UserId => GetFirstClaimValue(
        ClaimTypes.NameIdentifier,
        "oid",
        "sub",
        "preferred_username",
        ClaimTypes.Email);

    public bool IsHelpdeskAdmin
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null) return false;

            if (user.IsInRole("HelpdeskAdmin") || user.IsInRole("helpdeskadmin")) return true;

            if (user.Claims.Any(c =>
                    (c.Type == ClaimTypes.Role || c.Type == "roles") &&
                    string.Equals(c.Value, "HelpdeskAdmin", StringComparison.OrdinalIgnoreCase)))
                return true;

            var scopes = user.FindFirst("scp")?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         ?? Array.Empty<string>();
            if (scopes.Contains("Helpdesk.Admin", StringComparer.OrdinalIgnoreCase)) return true;

            return false;
        }
    }

    private string? GetFirstClaimValue(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
