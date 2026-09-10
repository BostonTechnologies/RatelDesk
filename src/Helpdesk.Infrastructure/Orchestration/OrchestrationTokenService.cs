using System.Net.Http.Headers;
using System.Text.Json;
using Helpdesk.Application.Orchestration;
using Microsoft.Extensions.Caching.Memory;

namespace Helpdesk.Infrastructure.Orchestration;

public sealed class OrchestrationTokenService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache) : IOrchestrationTokenService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;

    public async Task<string> GetAccessTokenAsync(OrchestrationResolvedSettings settings, CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = settings.TokenEndpoint
            ?? (string.IsNullOrWhiteSpace(settings.Authority) ? null : $"{settings.Authority.TrimEnd('/')}/connect/token");
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
        {
            throw new InvalidOperationException("External orchestration token endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new InvalidOperationException("External orchestration client credentials are not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Scope))
        {
            throw new InvalidOperationException("External orchestration scope is not configured.");
        }

        var cacheKey = $"orchestration_token::{tokenEndpoint}::{settings.ClientId}::{settings.Scope}";
        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["scope"] = settings.Scope
        };

        request.Content = new FormUrlEncodedContent(form);
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token request failed ({(int)response.StatusCode}): {Truncate(content, 200)}");
        }

        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Token response did not include access_token.");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Token response returned an empty access_token.");
        }

        var expiresInSeconds = 300;
        if (doc.RootElement.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt32(out var value))
        {
            expiresInSeconds = Math.Max(value, 60);
        }

        _cache.Set(
            cacheKey,
            token,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresInSeconds - 60)));

        return token;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
