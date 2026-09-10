using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Helpdesk.Application.Services.AI;

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;
    private readonly bool _toleratePlaintextInDev;
    private readonly ILogger<DataProtectionSecretProtector> _logger;

    public DataProtectionSecretProtector(
        IDataProtectionProvider provider,
        IWebHostEnvironment env,
        ILogger<DataProtectionSecretProtector> logger)
    {
        // IMPORTANT: purpose must stay stable for protect/unprotect
        _protector = provider.CreateProtector("AIProviderKeys");
        _toleratePlaintextInDev = env.IsDevelopment();
        _logger = logger;
    }

    public string Protect(string plaintext)
        => string.IsNullOrEmpty(plaintext) ? plaintext : _protector.Protect(plaintext);

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return protectedValue;

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException ex)
        {
            if (_toleratePlaintextInDev)
            {
                _logger.LogWarning(ex,
                    "DataProtection unprotect failed in Development. Treating value as plaintext. " +
                    "Ensure API and Web share the same key ring and ApplicationName.");
                return protectedValue; // dev convenience only
            }

            throw new SecretReentryRequiredException(
                "Cannot decrypt stored secret. Ensure all services share the same DataProtection key ring " +
                "and ApplicationName, or re-enter/re-encrypt the secret.", ex);
        }
    }
}
