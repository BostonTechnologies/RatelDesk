using System.Security.Claims;
using Helpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.API.Authentication;

/// <summary>
/// Validates a gateway-delegated execution token. It always reloads the
/// source credential and account so revocation, expiry, account disablement,
/// and the current access projection take effect on the next API request.
/// </summary>
public sealed class McpExecutionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    McpExecutionTokenService executionTokens,
    RatelDeskIdentityDbContext identityDb)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpExecution";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization["Bearer ".Length..].Trim();
        if (!executionTokens.TryRead(token, out var payload) || payload is null)
            return AuthenticateResult.Fail("The delegated MCP execution credential is invalid.");

        var credential = await identityDb.IntegrationCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == payload.CredentialId, Context.RequestAborted)
            .ConfigureAwait(false);
        if (credential is null || credential.RevokedAtUtc is not null || credential.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !string.Equals(credential.Purpose, IntegrationCredentialAuthenticationHandler.McpPurpose, StringComparison.Ordinal) ||
            !string.Equals(credential.OwnerUserId, payload.OwnerUserId, StringComparison.Ordinal) ||
            !string.Equals(credential.McpResourceUri, payload.ResourceUri, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("The delegated MCP execution credential is no longer valid.");
        }

        var owner = await identityDb.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == credential.OwnerUserId, Context.RequestAborted)
            .ConfigureAwait(false);
        if (owner?.IsEnabled != true)
            return AuthenticateResult.Fail("The delegated MCP execution credential owner is disabled.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.Id),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(owner.DisplayName) ? owner.UserName ?? owner.Email ?? owner.Id : owner.DisplayName),
            new(ClaimTypes.Email, owner.Email ?? string.Empty),
            new("auth_mode", "integration"),
            new("token_use", "mcp_execution"),
            new("integration_credential_id", credential.Id.ToString("N")),
            new("integration_purpose", credential.Purpose),
            new("mcp_resource_uri", credential.McpResourceUri ?? string.Empty)
        };
        if (!string.IsNullOrWhiteSpace(credential.OrganizationId))
            claims.Add(new Claim("integration_organization_id", credential.OrganizationId));
        foreach (var permission in credential.Permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim("integration_permission", permission));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
