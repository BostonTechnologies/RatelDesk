using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Orchestration;
using Helpdesk.Shared.DTOs.Orchestration;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.Orchestration;

public sealed class OrchestrationInternalClient(
    IHttpClientFactory httpClientFactory,
    IOrchestrationTokenService tokenService,
    ILogger<OrchestrationInternalClient> logger) : IOrchestrationInternalClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOrchestrationTokenService _tokenService = tokenService;
    private readonly ILogger<OrchestrationInternalClient> _logger = logger;

    public async Task<OrchestrationHealthResult> HealthAsync(OrchestrationResolvedSettings settings, CancellationToken cancellationToken = default)
    {
        var endpoint = BuildAbsoluteUri(settings.BaseUrl, settings.HealthPath);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        await AttachAuthHeaderAsync(settings, request, cancellationToken);

        var client = _httpClientFactory.CreateClient("OrchestrationInternalApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var message = response.IsSuccessStatusCode
            ? (string.IsNullOrWhiteSpace(content) ? "OK" : Truncate(content, 400))
            : $"HTTP {(int)response.StatusCode}: {Truncate(content, 200)}";

        return new OrchestrationHealthResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            Message = message
        };
    }

    public async Task<OrchestrationIngestResult> IngestAsync(
        OrchestrationResolvedSettings settings,
        OrchestrationIngestRequest requestPayload,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildAbsoluteUri(settings.BaseUrl, settings.IngestPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestPayload)
        };

        await AttachAuthHeaderAsync(settings, request, cancellationToken);

        var client = _httpClientFactory.CreateClient("OrchestrationInternalApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"External orchestration ingest failed ({(int)response.StatusCode}): {ExtractErrorMessage(content)}");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return new OrchestrationIngestResult
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                Status = "Submitted"
            };
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<OrchestrationIngestResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.ExecutionId))
            {
                return parsed;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "The external orchestration provider returned a non-standard ingest response.");
        }

        try
        {
            using var json = JsonDocument.Parse(content);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("executionId", out var executionNode)
                && executionNode.ValueKind == JsonValueKind.String)
            {
                return new OrchestrationIngestResult
                {
                    RequestId = TryGetString(json.RootElement, "requestId", "orchestrationRequestId"),
                    RunId = TryGetString(json.RootElement, "runId", "orchestrationRunId"),
                    ExecutionId = executionNode.GetString() ?? Guid.NewGuid().ToString("N"),
                    Status = TryGetString(json.RootElement, "status") ?? "Submitted",
                    Message = TryGetString(json.RootElement, "message")
                };
            }
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "The external orchestration provider response could not be inspected for an execution identifier.");
        }

        return new OrchestrationIngestResult
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            Status = "Submitted"
        };
    }

    private async Task AttachAuthHeaderAsync(
        OrchestrationResolvedSettings settings,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _tokenService.GetAccessTokenAsync(settings, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static Uri BuildAbsoluteUri(string? baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("External orchestration base URL is not configured.");
        }

        return new Uri(new Uri(baseUrl, UriKind.Absolute), path);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private string ExtractErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            var message = TryGetString(root, "message", "detail", "title");
            if (!string.IsNullOrWhiteSpace(message))
            {
                return Truncate(message, 500);
            }
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "The external orchestration provider error response was not JSON.");
        }

        return Truncate(content, 500);
    }

    private static string? TryGetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var node)
                && node.ValueKind == JsonValueKind.String)
            {
                var value = node.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
