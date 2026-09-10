using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Orchestration;
using Helpdesk.Shared.DTOs.Orchestration;

namespace Helpdesk.Infrastructure.Orchestration;

public sealed class OrchestrationCatalogService(
    IHttpClientFactory httpClientFactory,
    IOrchestrationTokenService tokenService,
    IOrchestrationConnectivityService connectivityService) : IOrchestrationCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOrchestrationTokenService _tokenService = tokenService;
    private readonly IOrchestrationConnectivityService _connectivityService = connectivityService;

    public Task<IReadOnlyList<OrchestrationCatalogJobDto>> ListJobsAsync(CancellationToken cancellationToken = default)
        => GetCatalogAsync<OrchestrationCatalogJobDto>("jobs", cancellationToken);

    public Task<IReadOnlyList<OrchestrationCatalogTenantDto>> ListTenantsAsync(CancellationToken cancellationToken = default)
        => GetCatalogAsync<OrchestrationCatalogTenantDto>("tenants", cancellationToken);

    public Task<IReadOnlyList<OrchestrationCatalogRequestDefinitionDto>> ListRequestDefinitionsAsync(CancellationToken cancellationToken = default)
        => GetCatalogAsync<OrchestrationCatalogRequestDefinitionDto>("request-definitions", cancellationToken);

    public async Task<OrchestrationCatalogRequestDefinitionDto> CreateRequestDefinitionAsync(
        CreateOrchestrationCatalogRequestDefinitionDto requestPayload,
        CancellationToken cancellationToken = default)
    {
        var settings = await _connectivityService.GetResolvedOrchestrationSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("External orchestration connectivity is disabled.");
        }

        var endpoint = BuildAbsoluteUri(settings.BaseUrl, BuildCatalogPath(settings.CatalogPath, "request-definitions"));
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
            throw new InvalidOperationException($"External orchestration request-definition create failed ({(int)response.StatusCode}): {Truncate(content, 300)}");
        }

        var parsed = JsonSerializer.Deserialize<OrchestrationCatalogRequestDefinitionDto>(content, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("External orchestration returned an empty response for request-definition creation.");
        }

        return parsed;
    }

    public async Task<OrchestrationCatalogRequestDefinitionDto> SyncRequestDefinitionInputsAsync(
        string requestDefinitionId,
        IReadOnlyList<OrchestrationCatalogInputDefinitionDto> inputs,
        CancellationToken cancellationToken = default)
    {
        var settings = await _connectivityService.GetResolvedOrchestrationSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("External orchestration connectivity is disabled.");
        }

        var normalizedRequestDefinitionId = string.IsNullOrWhiteSpace(requestDefinitionId)
            ? throw new InvalidOperationException("Request definition id is required.")
            : requestDefinitionId.Trim();

        var endpoint = BuildAbsoluteUri(settings.BaseUrl, BuildCatalogPath(settings.CatalogPath, $"request-definitions/{normalizedRequestDefinitionId}/inputs/sync"));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                Inputs = inputs
            })
        };

        await AttachAuthHeaderAsync(settings, request, cancellationToken);

        var client = _httpClientFactory.CreateClient("OrchestrationInternalApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"External orchestration request-definition input sync failed ({(int)response.StatusCode}): {Truncate(content, 300)}");
        }

        var parsed = JsonSerializer.Deserialize<OrchestrationCatalogRequestDefinitionDto>(content, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("External orchestration returned an empty response for request-definition input sync.");
        }

        return parsed;
    }

    private async Task<IReadOnlyList<T>> GetCatalogAsync<T>(string path, CancellationToken cancellationToken)
    {
        var settings = await _connectivityService.GetResolvedOrchestrationSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return [];
        }

        var endpoint = BuildAbsoluteUri(settings.BaseUrl, BuildCatalogPath(settings.CatalogPath, path));
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        await AttachAuthHeaderAsync(settings, request, cancellationToken);

        var client = _httpClientFactory.CreateClient("OrchestrationInternalApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"External orchestration catalog request failed ({(int)response.StatusCode}): {Truncate(content, 300)}");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var parsed = JsonSerializer.Deserialize<List<T>>(content, JsonOptions);
        return parsed ?? [];
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

    private static string BuildCatalogPath(string catalogPath, string suffix)
        => $"{catalogPath.TrimEnd('/')}/{suffix.TrimStart('/')}";

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
