namespace Helpdesk.Shared.Services;

/// <summary>
/// Determines whether an email address should be rejected during ingestion.
/// </summary>
public interface IEmailBlacklistService
{
    /// <summary>
    /// Returns <c>true</c> if the email is blacklisted.
    /// </summary>
    bool IsBlacklisted(string email);
}
