using Helpdesk.Shared.DTOs.Orchestration;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Orchestration;

public sealed class OrchestrationResolvedSettings
{
    public bool Enabled { get; init; }
    public string? BaseUrl { get; init; }
    public string? Audience { get; init; }
    public string? Authority { get; init; }
    public string? TokenEndpoint { get; init; }
    public string? Scope { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? RemoteSystemName { get; init; }
    public string HealthPath { get; init; } = "/api/v1/health";
    public string IngestPath { get; init; } = "/api/v1/orchestration/ingest";
    public string CatalogPath { get; init; } = "/api/v1/orchestration/catalog";
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class OrchestrationHealthResult
{
    public bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class RequestTaskPayloadBuildResult
{
    public bool Success { get; init; }
    public string PayloadJson { get; init; } = "{}";
    public string JobName { get; init; } = string.Empty;
    public string? AutomationBindingId { get; init; }
    public string? OrchestrationRequestDefinitionId { get; init; }
    public string? OrchestrationJobDefinitionId { get; init; }
    public string? Error { get; init; }

    public static RequestTaskPayloadBuildResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };

    public static RequestTaskPayloadBuildResult Succeeded(
        string jobName,
        string payloadJson,
        string? automationBindingId = null,
        string? orchestrationRequestDefinitionId = null,
        string? orchestrationJobDefinitionId = null) => new()
        {
            Success = true,
            JobName = jobName,
            PayloadJson = payloadJson,
            AutomationBindingId = automationBindingId,
            OrchestrationRequestDefinitionId = orchestrationRequestDefinitionId,
            OrchestrationJobDefinitionId = orchestrationJobDefinitionId
        };
}

public interface IOrchestrationConnectivityService
{
    Task<OrchestrationConnectivitySettingsDto> GetOrchestrationSettingsAsync(CancellationToken cancellationToken = default);
    Task<OrchestrationConnectivityTestResultDto> TestOrchestrationConnectivityAsync(CancellationToken cancellationToken = default);
    Task<OrchestrationResolvedSettings> GetResolvedOrchestrationSettingsAsync(CancellationToken cancellationToken = default);
}

public interface IOrchestrationTokenService
{
    Task<string> GetAccessTokenAsync(OrchestrationResolvedSettings settings, CancellationToken cancellationToken = default);
}

public interface IOrchestrationInternalClient
{
    Task<OrchestrationHealthResult> HealthAsync(OrchestrationResolvedSettings settings, CancellationToken cancellationToken = default);
    Task<OrchestrationIngestResult> IngestAsync(OrchestrationResolvedSettings settings, OrchestrationIngestRequest request, CancellationToken cancellationToken = default);
}

public interface IOrchestrationCatalogService
{
    Task<IReadOnlyList<OrchestrationCatalogJobDto>> ListJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrchestrationCatalogTenantDto>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrchestrationCatalogRequestDefinitionDto>> ListRequestDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<OrchestrationCatalogRequestDefinitionDto> CreateRequestDefinitionAsync(CreateOrchestrationCatalogRequestDefinitionDto request, CancellationToken cancellationToken = default);
    Task<OrchestrationCatalogRequestDefinitionDto> SyncRequestDefinitionInputsAsync(
        string requestDefinitionId,
        IReadOnlyList<OrchestrationCatalogInputDefinitionDto> inputs,
        CancellationToken cancellationToken = default);
}

public interface IRequestTaskPayloadBuilder
{
    Task<RequestTaskPayloadBuildResult> BuildAsync(
        RequestTask task,
        string correlationId,
        CancellationToken cancellationToken = default);
}
