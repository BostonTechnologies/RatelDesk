using HelpDesk.NewWeb.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HelpDesk.NewWeb.Services;

public interface IAuthentikAiAgentTokenValidator
{
    Task<AiAgentValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record AiAgentValidationResult(ClaimsPrincipal Principal, DateTimeOffset ExpiresAt);

public sealed class AuthentikAiAgentTokenValidator : IAuthentikAiAgentTokenValidator
{
    private readonly AuthentikAiAgentOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public AuthentikAiAgentTokenValidator(IOptions<AuthentikAiAgentOptions> options)
    {
        _options = options.Value;

        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.Authority))
        {
            var metadataAddress = $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration";
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
        }
    }

    public async Task<AiAgentValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new SecurityTokenValidationException("Authentik AI agent auth is disabled.");
        }

        if (_configurationManager is null)
        {
            throw new SecurityTokenValidationException("Authentik AI agent discovery is not configured.");
        }

        var oidc = await _configurationManager.GetConfigurationAsync(cancellationToken);

        var authority = _options.Authority.TrimEnd('/');
        var principal = _tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { authority, $"{authority}/" },
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidc.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        }, out var validatedToken);

        EnsureRequiredGroups(principal, _options.RequiredGroups);

        var identity = new ClaimsIdentity(
            principal.Claims.Select(c => new Claim(c.Type, c.Value)),
            CookieAuthenticationDefaults.AuthenticationScheme,
            "preferred_username",
            ClaimTypes.Role);

        foreach (var role in _options.SessionRoles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            identity.AddClaim(new Claim("roles", role));
        }

        identity.AddClaim(new Claim("auth_mode", "ai_agent"));
        identity.AddClaim(new Claim("identity_provider", "authentik_ai_agent"));

        var expiresAt = validatedToken switch
        {
            JwtSecurityToken jwt => new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero),
            JsonWebToken jsonWebToken => new DateTimeOffset(jsonWebToken.ValidTo, TimeSpan.Zero),
            _ => DateTimeOffset.UtcNow.AddMinutes(15)
        };

        return new AiAgentValidationResult(new ClaimsPrincipal(identity), expiresAt);
    }

    private static void EnsureRequiredGroups(ClaimsPrincipal principal, IEnumerable<string> requiredGroups)
    {
        var actualGroups = principal.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingGroups = requiredGroups
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Where(g => !actualGroups.Contains(g))
            .ToArray();

        if (missingGroups.Length > 0)
        {
            throw new SecurityTokenValidationException(
                $"Authentik AI agent token is missing required groups: {string.Join(", ", missingGroups)}");
        }
    }
}
