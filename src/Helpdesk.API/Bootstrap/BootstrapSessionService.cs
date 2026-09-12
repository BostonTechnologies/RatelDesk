using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Helpdesk.API.Bootstrap;

public sealed class BootstrapSessionService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sessions = new(StringComparer.Ordinal);

    public bool TryCreate(BootstrapDescriptor descriptor, string? setupCode, out string? session)
    {
        session = null;
        if (descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired ||
            string.IsNullOrWhiteSpace(setupCode) ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(descriptor.SetupCodeHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(setupCode.Trim()))))
        {
            return false;
        }

        session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[session] = DateTimeOffset.UtcNow.AddMinutes(10);
        return true;
    }

    public bool IsValid(string? session)
    {
        if (string.IsNullOrWhiteSpace(session) ||
            !_sessions.TryGetValue(session, out var expiresAtUtc))
        {
            return false;
        }

        if (expiresAtUtc > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _sessions.TryRemove(session, out _);
        return false;
    }
}
