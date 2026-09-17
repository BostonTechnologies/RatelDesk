namespace Helpdesk.Infrastructure.Identity;

/// <summary>
/// A revocable, opaque credential owned by one application account. Only a
/// SHA-256 verifier is persisted; the displayed secret is never recoverable.
/// </summary>
public sealed class IntegrationCredential
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
