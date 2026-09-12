using Microsoft.AspNetCore.Identity;

namespace Helpdesk.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? DisabledAtUtc { get; set; }

    public long AuthorizationRevision { get; set; }
}
