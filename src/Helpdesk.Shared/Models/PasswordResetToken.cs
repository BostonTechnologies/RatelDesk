namespace Helpdesk.Shared.Models;

/// <summary>
/// Represents a token used for resetting a user's password.
/// </summary>
/// <remarks>A password reset token is typically associated with a user's email address and has an expiration
/// time. This class encapsulates the token's unique identifier, the email address it is tied to, and the expiration
/// timestamp.</remarks>
public class PasswordResetToken
{
    public string Id { get; set; } = string.Empty; // token value
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
