using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Helpdesk.API.Bootstrap;

public sealed class BootstrapSessionService
{
    private readonly ConcurrentDictionary<string, SetupSession> _sessions = new(StringComparer.Ordinal);

    public bool TryCreate(BootstrapDescriptor descriptor, string? setupCode, out string? session)
    {
        session = null;
        var normalizedSetupCode = setupCode?.Trim().TrimStart('\uFEFF');
        if (descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired ||
            string.IsNullOrWhiteSpace(normalizedSetupCode) ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(descriptor.SetupCodeHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSetupCode))))
        {
            return false;
        }

        session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[session] = new SetupSession(DateTimeOffset.UtcNow.AddMinutes(10), descriptor.SetupCodeHash);
        return true;
    }

    public bool IsValid(string? session, BootstrapDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(session) ||
            !_sessions.TryGetValue(session, out var setupSession))
        {
            return false;
        }

        if (setupSession.ExpiresAtUtc > DateTimeOffset.UtcNow &&
            CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(setupSession.SetupCodeHash),
                Convert.FromHexString(descriptor.SetupCodeHash)))
        {
            return true;
        }

        _sessions.TryRemove(session, out _);
        return false;
    }

    private sealed record SetupSession(DateTimeOffset ExpiresAtUtc, string SetupCodeHash);
}
