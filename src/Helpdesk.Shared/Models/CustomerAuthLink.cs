using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class CustomerAuthLink
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public string AuthProviderType { get; set; } = "Authentik";
    public string? OidcIssuer { get; set; }
    public string? OidcSubject { get; set; }
    public string? LocalAccountId { get; set; }
    public string? AuthentikUserId { get; set; }
    public string? AuthentikUsername { get; set; }
    public string? AuthentikEmail { get; set; }
    public CustomerInviteStatus InviteStatus { get; set; } = CustomerInviteStatus.NotInvited;
    public DateTimeOffset? InviteSentAtUtc { get; set; }
    public DateTimeOffset? InviteAcceptedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public string? InvitedByUserId { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public string? DisabledByUserId { get; set; }
    public DateTimeOffset? LastAuthSyncAtUtc { get; set; }
    public string? LastAuthError { get; set; }
    public DateTimeOffset? InviteLinkExpiresAtUtc { get; set; }
}
