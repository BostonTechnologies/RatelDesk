using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Helpdesk.API.Authentication;

/// <summary>
/// Creates API-only, short-lived execution credentials after an HTTP MCP
/// gateway has authenticated an MCP-purpose integration credential. The opaque
/// value is protected by the API key ring, so a gateway can forward it but can
/// neither mint nor inspect it.
/// </summary>
public sealed class McpExecutionTokenService(IDataProtectionProvider protection)
{
    public const string TokenPrefix = "rdx_";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector = protection.CreateProtector("RatelDesk.Mcp.ExecutionCredential.v1");

    public string Create(IntegrationCredential credential, ApplicationUser owner, string resourceUri, DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);
        var payload = new McpExecutionTokenPayload(
            credential.Id,
            owner.Id,
            resourceUri,
            expiresAtUtc,
            Guid.NewGuid().ToString("N"));
        var protectedPayload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions));
        return TokenPrefix + WebEncoders.Base64UrlEncode(protectedPayload);
    }

    public bool TryRead(string? token, out McpExecutionTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var protectedPayload = WebEncoders.Base64UrlDecode(token[TokenPrefix.Length..]);
            payload = JsonSerializer.Deserialize<McpExecutionTokenPayload>(_protector.Unprotect(protectedPayload), SerializerOptions);
            return payload is not null && payload.ExpiresAtUtc > DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }
}

public sealed record McpExecutionTokenPayload(
    Guid CredentialId,
    string OwnerUserId,
    string ResourceUri,
    DateTimeOffset ExpiresAtUtc,
    string TokenId);
