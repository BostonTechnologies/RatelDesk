namespace Helpdesk.Infrastructure.Configuration;

public sealed class OrchestrationM2MOptions
{
    public bool Enabled { get; set; }
    public string ProviderName { get; set; } = "External orchestration provider";
    public string? BaseUrl { get; set; }
    public string? Audience { get; set; }
    public string? Scope { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string HealthPath { get; set; } = "/api/v1/health";
    public string IngestPath { get; set; } = "/api/v1/orchestration/ingest";
    public string CatalogPath { get; set; } = "/api/v1/orchestration/catalog";
    public string[] AllowedCallerClientIds { get; set; } = Array.Empty<string>();
}
