using System.Text.Json.Serialization;

namespace Helpdesk.Shared.DTOs.Orchestration;

public sealed class OrchestrationCatalogInputDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    public string? OptionsJson { get; set; }
    public int Order { get; set; }
}

public sealed class OrchestrationCatalogJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "/";
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    [JsonPropertyName("tenant_id")]
    public int? TenantIdSnake { get => TenantId; set => TenantId = value; }
    [JsonPropertyName("tenant_name")]
    public string? TenantNameSnake { get => TenantName; set => TenantName = value; }
    public int? OrchestrationTenantId { get => TenantId; set => TenantId = value; }
    public string? OrchestrationTenantName { get => TenantName; set => TenantName = value; }
    public string? Description { get; set; }
    public string ClientIdentity { get; set; } = string.Empty;
    public string? ClientDisplayName { get; set; }
    public string? ClientHostName { get; set; }
    public string? ClientName { get; set; }
    public string? ClientShortId { get; set; }
    public string? ScriptType { get; set; }
    public IReadOnlyList<OrchestrationCatalogInputDefinitionDto> Inputs { get; set; } = [];
    public int ExpectedRuntimeSeconds { get; set; } = 1800;
    public int GraceSeconds { get; set; }
    public int HardTimeoutSeconds { get; set; } = 1800;
}

public sealed class OrchestrationCatalogRequestDefinitionDto
{
    public string RequestDefinitionId { get; set; } = string.Empty;
    public string RequestDefinitionName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FolderPath { get; set; } = "/";
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    [JsonPropertyName("tenant_id")]
    public int? TenantIdSnake { get => TenantId; set => TenantId = value; }
    [JsonPropertyName("tenant_name")]
    public string? TenantNameSnake { get => TenantName; set => TenantName = value; }
    public int? OrchestrationTenantId { get => TenantId; set => TenantId = value; }
    public string? OrchestrationTenantName { get => TenantName; set => TenantName = value; }
    public string? OrchestrationJobDefinitionId { get; set; }
    public string? OrchestrationJobDefinitionName { get; set; }
    public string ClientIdentity { get; set; } = string.Empty;
    public string? ClientDisplayName { get; set; }
    public string? ClientHostName { get; set; }
    public string? ClientName { get; set; }
    public string? ClientShortId { get; set; }
    public string? ScriptType { get; set; }
    public IReadOnlyList<OrchestrationCatalogInputDefinitionDto> Inputs { get; set; } = [];
    public int ExpectedRuntimeSeconds { get; set; } = 1800;
    public int GraceSeconds { get; set; }
    public int HardTimeoutSeconds { get; set; } = 1800;
}

public sealed class OrchestrationCatalogTenantDto
{
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class CreateOrchestrationCatalogRequestDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "/";
    public string? Description { get; set; }
    public int? TenantId { get; set; }
    public string ClientIdentity { get; set; } = string.Empty;
}
