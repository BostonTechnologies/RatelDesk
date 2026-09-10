namespace Helpdesk.Shared.Models;

/// <summary>
/// Represents a two-factor authentication code associated with a user.
/// </summary>
/// <remarks>This class encapsulates the details of a two-factor authentication code, including its unique
/// identifier, the associated email address, and the expiration time. It is typically used to verify user identity
/// during authentication processes.</remarks>
public class TwoFactorCode
{
    public string Id { get; set; } = string.Empty; // code
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
