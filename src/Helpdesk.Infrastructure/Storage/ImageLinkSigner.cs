using System.Security.Cryptography;
using System.Text;
using Helpdesk.Application.WorkLogs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Storage;

public sealed class ImageLinkSigner(
    IOptions<StorageOptions> options,
    ILogger<ImageLinkSigner> logger) : IImageLinkSigner
{
    private readonly ILogger<ImageLinkSigner> _logger = logger;
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(
        !string.IsNullOrWhiteSpace(options.Value.ImageSigningSecret)
            ? options.Value.ImageSigningSecret
            : throw new InvalidOperationException("Configuration value 'StorageOptions:ImageSigningSecret' is required."));

    public string GenerateToken(string scope, string id, string filename, DateTimeOffset expires)
    {
        var expiryUnix = expires.ToUnixTimeSeconds();
        var payload = $"{scope}|{id}|{filename}|{expiryUnix}";
        var signature = ComputeSignature(payload);
        var token = $"{payload}|{signature}";
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public bool ValidateToken(string token, string scope, string id, string filename)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Inline image token missing. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            _logger.LogWarning("Inline image token decode failed. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        var parts = decoded.Split('|');
        if (parts.Length != 5)
        {
            _logger.LogWarning("Inline image token malformed. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        if (!string.Equals(parts[0], scope, StringComparison.Ordinal) ||
            !string.Equals(parts[1], id, StringComparison.Ordinal) ||
            !string.Equals(parts[2], filename, StringComparison.Ordinal))
        {
            _logger.LogWarning("Inline image token payload mismatch. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        if (!long.TryParse(parts[3], out var expiryUnix))
        {
            _logger.LogWarning("Inline image token expiry invalid. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        if (DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(expiryUnix))
        {
            _logger.LogWarning("Inline image token expired. Scope={Scope} Id={Id} File={File}", scope, id, filename);
            return false;
        }

        var payload = $"{parts[0]}|{parts[1]}|{parts[2]}|{parts[3]}";
        var expectedSignature = ComputeSignature(payload);
        var valid = FixedTimeEquals(parts[4], expectedSignature);
        if (!valid)
        {
            _logger.LogWarning("Inline image token signature invalid. Scope={Scope} Id={Id} File={File}", scope, id, filename);
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
