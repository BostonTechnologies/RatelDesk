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
    /// <summary>
    /// Canonical HTTP MCP resource this credential is paired to. This is set
    /// only for <c>mcp</c>-purpose credentials and prevents a bearer copied to
    /// another gateway from being delegated there.
    /// </summary>
    public string? McpResourceUri { get; set; }
    public string? OrganizationId { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>
    /// Provider-neutral UTC sort key for credential listings. SQLite cannot
    /// translate ordering over <see cref="DateTimeOffset"/> values.
    /// </summary>
    public long CreatedAtUnixMilliseconds { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
