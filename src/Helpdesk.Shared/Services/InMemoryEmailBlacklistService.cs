using System.Collections.Generic;

namespace Helpdesk.Shared.Services;

/// <summary>
/// Simple in-memory blacklist implementation.
/// </summary>
public class InMemoryEmailBlacklistService : IEmailBlacklistService
{
    private readonly HashSet<string> _blocked = new();

    /// <summary>
    /// Adds an address to the blacklist.
    /// </summary>
    public void Add(string email) => _blocked.Add(email.ToLowerInvariant());

    /// <inheritdoc />
    public bool IsBlacklisted(string email) => _blocked.Contains(email.ToLowerInvariant());
}
