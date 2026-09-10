using System.Security.Cryptography;
using System.Text;
using Helpdesk.Application.Tickets;
using Helpdesk.Infrastructure.Storage;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Security;

public sealed class PublicTicketLinkSigner(
    IOptions<StorageOptions> options,
    ILogger<PublicTicketLinkSigner> logger) : IPublicTicketLinkSigner
{
    private readonly ILogger<PublicTicketLinkSigner> _logger = logger;
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(
        !string.IsNullOrWhiteSpace(options.Value.ImageSigningSecret)
            ? options.Value.ImageSigningSecret
            : throw new InvalidOperationException("Configuration value 'StorageOptions:ImageSigningSecret' is required."));

    public string GenerateToken(string trackingId, string email, DateTimeOffset expires)
    {
        var expiryUnix = expires.ToUnixTimeSeconds();
        var payload = $"{trackingId}|{email}|{expiryUnix}";
        var signature = ComputeSignature(payload);
        var token = $"{payload}|{signature}";
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public bool ValidateToken(string token, string trackingId, string email)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Public ticket token missing. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            _logger.LogWarning("Public ticket token decode failed. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        var parts = decoded.Split('|');
        if (parts.Length != 4)
        {
            _logger.LogWarning("Public ticket token malformed. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        if (!string.Equals(parts[0], trackingId, StringComparison.Ordinal) ||
            !string.Equals(parts[1], email, StringComparison.Ordinal))
        {
            _logger.LogWarning("Public ticket token payload mismatch. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        if (!long.TryParse(parts[2], out var expiryUnix))
        {
            _logger.LogWarning("Public ticket token expiry invalid. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        if (DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(expiryUnix))
        {
            _logger.LogWarning("Public ticket token expired. TrackingId={TrackingId} Email={Email}", trackingId, email);
            return false;
        }

        var payload = $"{parts[0]}|{parts[1]}|{parts[2]}";
        var expectedSignature = ComputeSignature(payload);
        var valid = FixedTimeEquals(parts[3], expectedSignature);
        if (!valid)
        {
            _logger.LogWarning("Public ticket token signature invalid. TrackingId={TrackingId} Email={Email}", trackingId, email);
        }

        return valid;
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return WebEncoders.Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
