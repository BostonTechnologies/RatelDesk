using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Orchestration;

public sealed class OrchestrationCallbackRejectionOpsDto
{
    public Guid NotificationId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? CallerClientId { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class AutomationBindingIssueOpsDto
{
    public string BindingId { get; set; } = string.Empty;
    public string RequestFormId { get; set; } = string.Empty;
    public string RequestFormTitle { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public Guid TaskTemplateId { get; set; }
    public string? TaskName { get; set; }
    public string OrchestrationRequestDefinitionId { get; set; } = string.Empty;
    public string? OrchestrationRequestDefinitionName { get; set; }
    public string? OrchestrationJobDefinitionId { get; set; }
    public string? OrchestrationJobDefinitionName { get; set; }
    public AutomationBindingSyncState SyncState { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
