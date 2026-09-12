using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HelpDesk.NewWeb.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace HelpDesk.NewWeb.Services;

public interface ITokenService
{
    Task<string?> GetValidAccessTokenAsync();
    Task<bool> TryRefreshSessionAsync(HttpContext ctx, AuthenticateResult auth, CancellationToken cancellationToken = default);
}

public class TokenService : ITokenService
{
    private const string RefreshedAccessTokenItemKey = "Helpdesk.RefreshedAccessToken";
    internal const string SessionRefreshedItemKey = "Helpdesk.SessionRefreshed";
    private static readonly TimeSpan HelpdeskSessionLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan RecentRefreshResultLifetime = TimeSpan.FromSeconds(30);
    private const int RefreshLockStripeCount = 64;
    private const int MaxRecentRefreshResults = 1024;
    private static readonly SemaphoreSlim[] RefreshLocks = Enumerable
        .Range(0, RefreshLockStripeCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();
    private static readonly ConcurrentDictionary<string, CachedRefreshResult> RecentRefreshResults = new(StringComparer.Ordinal);
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISystemTokenService _systemTokenService;
    private readonly ILogger<TokenService> _logger;

    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    public TokenService(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISystemTokenService systemTokenService,
        ILogger<TokenService>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _systemTokenService = systemTokenService;
        _logger = logger ?? NullLogger<TokenService>.Instance;
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null || ctx.User?.Identity?.IsAuthenticated != true)
            return null;

        var auth = await ctx.AuthenticateAsync();
        if (!auth.Succeeded)
            return null;

        // Local sessions are authenticated by the shared API/Web data-protection
        // cookie. They do not carry OIDC access or refresh tokens.
        if (IsLocalSession(auth.Principal ?? ctx.User))
            return null;

        var refreshed = await TryRefreshSessionAsync(ctx, auth, ctx.RequestAborted);
        if (!refreshed)
            return null;

        if (ctx.Items.TryGetValue(RefreshedAccessTokenItemKey, out var refreshedAccessToken)
            && refreshedAccessToken is string refreshedToken
            && !string.IsNullOrWhiteSpace(refreshedToken))
        {
            return refreshedToken;
        }

        return auth.Properties?.GetTokenValue("access_token")
            ?? await ctx.GetTokenAsync("access_token");
    }

    public async Task<bool> TryRefreshSessionAsync(HttpContext ctx, AuthenticateResult auth, CancellationToken cancellationToken = default)
    {
        var properties = auth.Properties ?? new AuthenticationProperties();
        if (auth.Properties is null && auth.Principal is not null)
        {
            auth = AuthenticateResult.Success(new AuthenticationTicket(
                auth.Principal,
                properties,
                CookieAuthenticationDefaults.AuthenticationScheme));
        }

        var sessionPrincipal = auth.Principal ?? ctx.User;
        if (IsLocalSession(sessionPrincipal))
            return true;

        if (IsDevelopmentOperator(sessionPrincipal, properties))
            return await RefreshDevelopmentOperatorAsync(ctx, auth);

        if (IsAiAgentSession(sessionPrincipal))
            return await ValidateAiAgentSessionAsync(ctx, auth);

        var accessToken = properties.GetTokenValue("access_token");
        var expiresAtString = properties.GetTokenValue("expires_at");
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(expiresAtString))
        {
            _logger.LogWarning(
                "OIDC session is missing token state for {User}. AccessTokenPresent={AccessTokenPresent} ExpiresAtPresent={ExpiresAtPresent} CookieExpiresUtc={CookieExpiresUtc}",
                GetUserLabel(ctx.User),
                !string.IsNullOrWhiteSpace(accessToken),
                !string.IsNullOrWhiteSpace(expiresAtString),
                properties.ExpiresUtc);
            await SignOutSessionAsync(ctx);
            return false;
        }

        if (!DateTime.TryParse(expiresAtString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
        {
            _logger.LogWarning(
                "OIDC session has an invalid expires_at token value for {User}. CookieExpiresUtc={CookieExpiresUtc}",
                GetUserLabel(ctx.User),
                properties.ExpiresUtc);
            await SignOutSessionAsync(ctx);
            return false;
        }

        if (DateTime.UtcNow < expiresAt.Subtract(TokenRefreshBuffer))
            return true;

        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning(
                "OIDC session cannot refresh because refresh_token is missing for {User}. TokenExpiresAt={TokenExpiresAt} CookieExpiresUtc={CookieExpiresUtc}",
                GetUserLabel(ctx.User),
                expiresAt,
                properties.ExpiresUtc);
            await SignOutSessionAsync(ctx);
            return false;
        }

        var refreshedAccessToken = await RefreshOidcWithGuardAsync(ctx, auth, accessToken, refreshToken, expiresAt, cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
        {
            await SignOutSessionAsync(ctx);
            return false;
        }

        ctx.Items[RefreshedAccessTokenItemKey] = refreshedAccessToken;
        return true;
    }

    private async Task<string?> RefreshOidcWithGuardAsync(
        HttpContext ctx,
        AuthenticateResult auth,
        string accessToken,
        string refreshToken,
        DateTime currentAccessTokenExpiresAt,
        CancellationToken cancellationToken)
    {
        var refreshKey = BuildRefreshKey(ctx.User, accessToken, refreshToken);
        PruneExpiredRefreshResults();

        var gate = GetRefreshLock(refreshKey);

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetRecentRefreshResult(refreshKey, out var recent))
            {
                _logger.LogInformation(
                    "Reusing recent OIDC refresh result for {User}. TokenExpiresAt={TokenExpiresAt} NewTokenExpiresAt={NewTokenExpiresAt} CookieExpiresUtc={CookieExpiresUtc}",
                    GetUserLabel(ctx.User),
                    currentAccessTokenExpiresAt,
                    recent.AccessTokenExpiresAt,
                    auth.Properties?.ExpiresUtc);

                await UpdateCookieTokensAsync(
                    ctx,
                    auth,
                    recent.AccessToken,
                    recent.RefreshToken,
                    recent.ExpiresIn,
                    alignTicketExpiryToAccessToken: false);

                return recent.AccessToken;
            }

            var outcome = await RefreshOidcAsync(ctx, auth, refreshToken, currentAccessTokenExpiresAt, cancellationToken);
            if (outcome.Success)
            {
                var cached = new CachedRefreshResult(
                    outcome.AccessToken!,
                    outcome.RefreshTokenToStore,
                    outcome.ExpiresIn,
                    outcome.AccessTokenExpiresAt,
                    DateTimeOffset.UtcNow.Add(RecentRefreshResultLifetime));
                RecentRefreshResults[refreshKey] = cached;
                _ = RemoveRecentRefreshResultAfterExpiryAsync(refreshKey, cached);
                PruneExpiredRefreshResults();

                return outcome.AccessToken;
            }

            if (TryGetRecentRefreshResult(refreshKey, out recent))
            {
                _logger.LogInformation(
                    "OIDC refresh failed after another refresh succeeded for {User}; reusing recent result. StatusCode={StatusCode} Error={Error}",
                    GetUserLabel(ctx.User),
                    outcome.StatusCode,
                    outcome.Error);

                await UpdateCookieTokensAsync(
                    ctx,
                    auth,
                    recent.AccessToken,
                    recent.RefreshToken,
                    recent.ExpiresIn,
                    alignTicketExpiryToAccessToken: false);

                return recent.AccessToken;
            }

            _logger.LogWarning(
                "OIDC refresh failed for {User}. StatusCode={StatusCode} Error={Error} TokenExpiresAt={TokenExpiresAt} CookieExpiresUtc={CookieExpiresUtc}",
                GetUserLabel(ctx.User),
                outcome.StatusCode,
                outcome.Error,
                currentAccessTokenExpiresAt,
                auth.Properties?.ExpiresUtc);

            return null;
        }
        finally
        {
            gate.Release();
            PruneExpiredRefreshResults();
        }
    }

    private static bool IsDevelopmentOperator(ClaimsPrincipal user, AuthenticationProperties properties)
    {
        if (user.Claims.Any(c => c.Type == "auth_mode" && string.Equals(c.Value, "development", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (user.Claims.Any(c => c.Type == "token_use" && string.Equals(c.Value, "system", StringComparison.OrdinalIgnoreCase)))
            return true;

        var accessToken = properties.GetTokenValue("access_token");
        return !string.IsNullOrWhiteSpace(accessToken)
            && string.Equals(ExtractClaim(accessToken, "auth_mode"), "development", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAiAgentSession(ClaimsPrincipal user)
    {
        return user.Claims.Any(c => c.Type == "auth_mode" && string.Equals(c.Value, "ai_agent", StringComparison.OrdinalIgnoreCase))
            || user.Claims.Any(c => c.Type == "identity_provider" && string.Equals(c.Value, "authentik_ai_agent", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLocalSession(ClaimsPrincipal user) =>
        user.Claims.Any(c => c.Type == "auth_mode" && string.Equals(c.Value, "local", StringComparison.OrdinalIgnoreCase));

    private static string? ExtractClaim(string jwt, string claimType)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        static string B64(string s) => s.Replace('-', '+').Replace('_', '/').PadRight((s.Length + 3) / 4 * 4, '=');

        var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(B64(parts[1])));
        var needle = $"\"{claimType}\":\"";
        var idx = payloadJson.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0) return null;

        idx += needle.Length;
        var end = payloadJson.IndexOf('"', idx);
        return end > idx ? payloadJson[idx..end] : null;
    }

    private async Task<RefreshOutcome> RefreshOidcAsync(
        HttpContext ctx,
        AuthenticateResult auth,
        string refreshToken,
        DateTime currentAccessTokenExpiresAt,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var oidcOptions = HumanOidcRuntimeOptionsResolver.Resolve(_configuration);

        var scopes = new[] { "openid", "profile", "email", "offline_access", oidcOptions.ApiScope }
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .SelectMany(scope => scope!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var scope = string.Join(' ', scopes);

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = oidcOptions.ClientId ?? string.Empty,
            ["refresh_token"] = refreshToken,
            ["scope"] = scope
        };

        if (!string.IsNullOrWhiteSpace(oidcOptions.ClientSecret))
            body["client_secret"] = oidcOptions.ClientSecret;

        using var req = new HttpRequestMessage(HttpMethod.Post, oidcOptions.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(body)
        };

        _logger.LogInformation(
            "Attempting OIDC refresh for {User}. TokenExpiresAt={TokenExpiresAt} CookieExpiresUtc={CookieExpiresUtc} RefreshTokenPresent={RefreshTokenPresent} Authority={Authority} ClientId={ClientId}",
            GetUserLabel(ctx.User),
            currentAccessTokenExpiresAt,
            auth.Properties?.ExpiresUtc,
            !string.IsNullOrWhiteSpace(refreshToken),
            oidcOptions.Authority,
            oidcOptions.ClientId);

        using var resp = await client.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            return RefreshOutcome.Failed(resp.StatusCode, resp.StatusCode.ToString());

        var tokenResponse = await resp.Content.ReadFromJsonAsync<OidcRefreshTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
            return RefreshOutcome.Failed(resp.StatusCode, "missing_access_token");

        var refreshTokenToStore = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? refreshToken
            : tokenResponse.RefreshToken;

        var accessTokenExpiresAt = await UpdateCookieTokensAsync(
            ctx,
            auth,
            tokenResponse.AccessToken,
            refreshTokenToStore,
            tokenResponse.ExpiresIn,
            alignTicketExpiryToAccessToken: false);

        _logger.LogInformation(
            "OIDC refresh succeeded for {User}. NewTokenExpiresAt={NewTokenExpiresAt} CookieExpiresUtc={CookieExpiresUtc} ReplacementRefreshTokenReturned={ReplacementRefreshTokenReturned}",
            GetUserLabel(ctx.User),
            accessTokenExpiresAt,
            auth.Properties?.ExpiresUtc,
            !string.IsNullOrWhiteSpace(tokenResponse.RefreshToken));

        return RefreshOutcome.Succeeded(
            tokenResponse.AccessToken,
            refreshTokenToStore,
            tokenResponse.ExpiresIn,
            accessTokenExpiresAt);
    }

    private static async Task<bool> ValidateAiAgentSessionAsync(HttpContext ctx, AuthenticateResult auth)
    {
        var properties = auth.Properties ?? new AuthenticationProperties();
        var expiresAtString = properties.GetTokenValue("expires_at");

        if (string.IsNullOrWhiteSpace(expiresAtString)
            || !DateTime.TryParse(expiresAtString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            || DateTime.UtcNow >= expiresAt)
        {
            await SignOutSessionAsync(ctx);
            return false;
        }

        return true;
    }

    private async Task<DateTimeOffset?> UpdateCookieTokensAsync(
        HttpContext ctx,
        AuthenticateResult auth,
        string accessToken,
        string? refreshToken,
        double? expiresInSeconds,
        bool alignTicketExpiryToAccessToken)
    {
        var properties = auth.Properties ?? new AuthenticationProperties();
        properties.UpdateTokenValue("access_token", accessToken);

        if (!string.IsNullOrWhiteSpace(refreshToken))
            properties.UpdateTokenValue("refresh_token", refreshToken);

        DateTimeOffset? newExpiresAt = null;
        if (expiresInSeconds is > 0)
        {
            newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds.Value);
            properties.UpdateTokenValue("expires_at", newExpiresAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));

            if (alignTicketExpiryToAccessToken)
            {
                properties.ExpiresUtc = newExpiresAt;
            }
            else
            {
                PreserveOrRestoreHelpdeskSessionLifetime(properties, newExpiresAt.Value);
            }
        }

        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, auth.Principal!, properties);
        ctx.Items[SessionRefreshedItemKey] = true;
        return newExpiresAt;
    }

    private async Task<bool> RefreshDevelopmentOperatorAsync(HttpContext ctx, AuthenticateResult auth)
    {
        var accessToken = await _systemTokenService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await SignOutSessionAsync(ctx);
            return false;
        }

        var properties = auth.Properties ?? new AuthenticationProperties();
        if (properties.ExpiresUtc <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            // Development sessions obtain a fresh system token for each request. Keeping it out of
            // the browser cookie avoids repeatedly serializing a bearer credential into the ticket.
            properties.StoreTokens([]);
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, auth.Principal!, properties);
            ctx.Items[SessionRefreshedItemKey] = true;
        }

        ctx.Items[RefreshedAccessTokenItemKey] = accessToken;
        return true;
    }

    private void PreserveOrRestoreHelpdeskSessionLifetime(AuthenticationProperties properties, DateTimeOffset accessTokenExpiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        var existingTicketExpiry = properties.ExpiresUtc;
        if (existingTicketExpiry.HasValue
            && existingTicketExpiry.Value > now
            && existingTicketExpiry.Value > accessTokenExpiresAt)
        {
            return;
        }

        properties.ExpiresUtc = now.Add(GetHelpdeskSessionLifetime());
    }

    private TimeSpan GetHelpdeskSessionLifetime()
    {
        var configured = _configuration.GetValue<TimeSpan?>("Authentication:Cookie:ExpireTimeSpan");
        return configured.GetValueOrDefault(HelpdeskSessionLifetime);
    }

    private static async Task SignOutSessionAsync(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static bool TryGetRecentRefreshResult(string refreshKey, out CachedRefreshResult result)
    {
        if (RecentRefreshResults.TryGetValue(refreshKey, out result!)
            && result.CachedUntil > DateTimeOffset.UtcNow)
        {
            return true;
        }

        RecentRefreshResults.TryRemove(refreshKey, out _);
        result = default!;
        return false;
    }

    private static SemaphoreSlim GetRefreshLock(string refreshKey)
    {
        var hash = StringComparer.Ordinal.GetHashCode(refreshKey) & int.MaxValue;
        return RefreshLocks[hash % RefreshLocks.Length];
    }

    private static async Task RemoveRecentRefreshResultAfterExpiryAsync(string refreshKey, CachedRefreshResult cached)
    {
        await Task.Delay(RecentRefreshResultLifetime);
        ((ICollection<KeyValuePair<string, CachedRefreshResult>>)RecentRefreshResults)
            .Remove(new KeyValuePair<string, CachedRefreshResult>(refreshKey, cached));
    }

    private static void PruneExpiredRefreshResults()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in RecentRefreshResults)
        {
            if (entry.Value.CachedUntil <= now)
                RecentRefreshResults.TryRemove(entry.Key, out _);
        }

        var overflow = RecentRefreshResults.Count - MaxRecentRefreshResults;
        if (overflow <= 0)
            return;

        foreach (var entry in RecentRefreshResults
            .OrderBy(entry => entry.Value.CachedUntil)
            .Take(overflow))
        {
            RecentRefreshResults.TryRemove(entry.Key, out _);
        }
    }

    private static string BuildRefreshKey(ClaimsPrincipal user, string accessToken, string refreshToken)
    {
        var userKey = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.Identity?.Name
            ?? "anonymous";
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{accessToken}:{refreshToken}")));
        return $"{userKey}:{tokenHash}";
    }

    private static string GetUserLabel(ClaimsPrincipal user)
    {
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.Identity?.Name
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? "unknown";
    }

    private sealed record CachedRefreshResult(
        string AccessToken,
        string? RefreshToken,
        double? ExpiresIn,
        DateTimeOffset? AccessTokenExpiresAt,
        DateTimeOffset CachedUntil);

    private sealed record RefreshOutcome(
        bool Success,
        string? AccessToken,
        string? RefreshTokenToStore,
        double? ExpiresIn,
        DateTimeOffset? AccessTokenExpiresAt,
        HttpStatusCode? StatusCode,
        string? Error)
    {
        public static RefreshOutcome Succeeded(
            string accessToken,
            string? refreshTokenToStore,
            double? expiresIn,
            DateTimeOffset? accessTokenExpiresAt)
            => new(true, accessToken, refreshTokenToStore, expiresIn, accessTokenExpiresAt, null, null);

        public static RefreshOutcome Failed(HttpStatusCode statusCode, string error)
            => new(false, null, null, null, null, statusCode, error);
    }

    private sealed class OidcRefreshTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? ExpiresIn { get; set; }
    }
}
