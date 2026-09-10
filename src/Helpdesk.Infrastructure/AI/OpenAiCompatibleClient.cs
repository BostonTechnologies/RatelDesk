using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public class OpenAiCompatibleClient(IHttpClientFactory httpClientFactory, ISecretProtector protector) : IAiClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ISecretProtector _protector = protector;

    private static void EnsureSupported(AiProvider provider)
    {
        if (provider.ProviderType != AiProviderType.OpenAI &&
            provider.ProviderType != AiProviderType.OpenAICompatible &&
            provider.ProviderType != AiProviderType.Groq)
        {
            throw new NotSupportedException($"Provider type {provider.ProviderType} is not supported.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, AiProvider provider, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var apiKey = _protector.Unprotect(provider.ApiKeyEncrypted);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (!string.IsNullOrWhiteSpace(provider.ExtraHeadersJson))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(provider.ExtraHeadersJson);
                if (headers is not null)
                {
                    foreach (var (key, value) in headers)
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore invalid headers JSON
            }
        }

        return request;
    }

    private static IReadOnlyList<Uri> BuildEndpointCandidates(AiProvider provider, string relativePath)
    {
        var baseUrl = (provider.BaseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Provider base URL is empty.");
        }

        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return [new Uri($"{normalized}/{relativePath}")];
        }

        return
        [
            new Uri($"{normalized}/v1/{relativePath}"),
            new Uri($"{normalized}/api/{relativePath}")
        ];
    }

    private static string? TruncateBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        var compact = body.Trim();
        return compact.Length <= 600 ? compact : compact[..600];
    }

    private static string BuildFailureMessage(int? statusCode, string? body)
    {
        return statusCode switch
        {
            null => "Connection failed before the provider returned a response.",
            _ when string.IsNullOrWhiteSpace(body) => $"Provider returned HTTP {statusCode}.",
            _ => $"Provider returned HTTP {statusCode}: {TruncateBody(body)}"
        };
    }

    private async Task<(JsonElement? Json, AiProviderConnectionTestResult Result)> TryGetJsonAsync(
        AiProvider provider,
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken token)
    {
        var client = _httpClientFactory.CreateClient("OpenAICompatible");
        AiProviderConnectionTestResult? lastFailure = null;

        foreach (var candidate in BuildEndpointCandidates(provider, relativePath))
        {
            try
            {
                using var request = CreateRequest(method, provider, candidate);
                if (payload is not null)
                {
                    request.Content = JsonContent.Create(payload);
                }

                using var response = await client.SendAsync(request, token);
                var body = await response.Content.ReadAsStringAsync(token);
                if (response.IsSuccessStatusCode)
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(body);
                    return (json, new AiProviderConnectionTestResult(
                        true,
                        candidate.ToString(),
                        (int)response.StatusCode,
                        "Connection OK."));
                }

                lastFailure = new AiProviderConnectionTestResult(
                    false,
                    candidate.ToString(),
                    (int)response.StatusCode,
                    BuildFailureMessage((int)response.StatusCode, body),
                    TruncateBody(body));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastFailure = new AiProviderConnectionTestResult(
                    false,
                    candidate.ToString(),
                    Message: ex.Message);
            }
        }

        return (null, lastFailure ?? new AiProviderConnectionTestResult(false, Message: "Connection failed."));
    }

    public async Task<IEnumerable<string>> ListModelsAsync(AiProvider provider, CancellationToken token)
    {
        EnsureSupported(provider);
        var (json, result) = await TryGetJsonAsync(provider, HttpMethod.Get, "models", payload: null, token);
        if (json is null)
        {
            throw new InvalidOperationException(result.Message ?? "Model discovery failed.");
        }

        return json.Value.GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetString() ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
    }

    public async Task<AiProviderConnectionTestResult> TestAsync(AiProvider provider, CancellationToken token)
    {
        EnsureSupported(provider);
        var (_, result) = await TryGetJsonAsync(provider, HttpMethod.Get, "models", payload: null, token);
        return result;
    }

    public async Task<string> ChatAsync(AiProvider provider, string prompt, string? model, CancellationToken token)
    {
        EnsureSupported(provider);
        var modelName = model ?? provider.DefaultModel ?? throw new ArgumentException("Model must be provided", nameof(model));
        var payload = new
        {
            model = modelName,
            messages = new[] { new { role = "user", content = prompt } },
            stream = false
        };

        var (json, result) = await TryGetJsonAsync(provider, HttpMethod.Post, "chat/completions", payload, token);
        if (json is null)
        {
            throw new InvalidOperationException(result.Message ?? "Chat test failed.");
        }

        return json.Value.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
