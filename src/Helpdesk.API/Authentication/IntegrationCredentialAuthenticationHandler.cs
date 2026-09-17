using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.API.Authentication;

public sealed class IntegrationCredentialAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    RatelDeskIdentityDbContext identityDb)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationCredential";
    public const string ApiPurpose = "api";
    public const string McpPurpose = "mcp";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer rdk_", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization["Bearer ".Length..].Trim();
        var segments = token.Split('_', 3, StringSplitOptions.None);
        if (segments.Length != 3 || !Guid.TryParseExact(segments[1], "N", out var credentialId) || string.IsNullOrWhiteSpace(segments[2]))
            return AuthenticateResult.Fail("The integration credential is malformed.");

        var credential = await identityDb.IntegrationCredentials
            .SingleOrDefaultAsync(candidate => candidate.Id == credentialId, Context.RequestAborted)
            .ConfigureAwait(false);
        if (credential is null ||
            credential.RevokedAtUtc is not null ||
            credential.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !string.Equals(credential.Purpose, ApiPurpose, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("The integration credential is not valid for this API.");
        }

        var providedVerifier = SHA256.HashData(Encoding.UTF8.GetBytes(segments[2]));
        var storedVerifier = Convert.FromHexString(credential.SecretHash);
        if (!CryptographicOperations.FixedTimeEquals(providedVerifier, storedVerifier))
            return AuthenticateResult.Fail("The integration credential is not valid.");

        var owner = await identityDb.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == credential.OwnerUserId, Context.RequestAborted)
            .ConfigureAwait(false);
        if (owner?.IsEnabled != true)
            return AuthenticateResult.Fail("The integration credential owner is disabled.");

        credential.LastUsedAtUtc = DateTimeOffset.UtcNow;
        await identityDb.SaveChangesAsync(Context.RequestAborted).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.Id),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(owner.DisplayName) ? owner.UserName ?? owner.Email ?? owner.Id : owner.DisplayName),
            new(ClaimTypes.Email, owner.Email ?? string.Empty),
            new("auth_mode", "integration"),
            new("integration_credential_id", credential.Id.ToString("N")),
            new("integration_purpose", credential.Purpose)
        };
        if (!string.IsNullOrWhiteSpace(credential.OrganizationId))
            claims.Add(new Claim("integration_organization_id", credential.OrganizationId));
        foreach (var permission in credential.Permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim("integration_permission", permission));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
