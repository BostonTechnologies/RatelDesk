using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Customer;

public class CustomerAuthStatusDto
{
    public string CustomerId { get; set; } = string.Empty;
    public CustomerInviteStatus InviteStatus { get; set; } = CustomerInviteStatus.NotInvited;
    public string StatusText { get; set; } = "Not invited";
    public string? AuthProviderType { get; set; }
    public string? OidcIssuer { get; set; }
    public string? OidcSubject { get; set; }
    public string? AuthentikUserId { get; set; }
    public string? AuthentikUsername { get; set; }
    public string? AuthentikEmail { get; set; }
    public DateTimeOffset? InviteSentAtUtc { get; set; }
    public DateTimeOffset? InviteAcceptedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public DateTimeOffset? LastAuthSyncAtUtc { get; set; }
    public string? LastAuthError { get; set; }
    public DateTimeOffset? InviteLinkExpiresAtUtc { get; set; }
    public bool CanInvite { get; set; }
    public bool CanResend { get; set; }
    public bool CanDisableLogin { get; set; }
}
