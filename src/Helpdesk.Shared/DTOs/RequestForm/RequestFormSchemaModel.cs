namespace Helpdesk.Shared.DTOs.RequestForm;

public sealed class RequestFormSchemaModel
{
    public List<FormField> Fields { get; set; } = new();
    public List<RequestTaskTemplateModel> Tasks { get; set; } = new();
}

public sealed class RequestTaskTemplateModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "New Task";
    public string? Description { get; set; }
    public int Order { get; set; }
    public string Type { get; set; } = "manual";
    public bool AutoStart { get; set; } = true;
    public string? DefaultAssigneeId { get; set; }
    public string? TeamOrRole { get; set; }
    public string? OrchestratorJobName { get; set; }
    public Dictionary<string, string>? PayloadMapping { get; set; }
    public int? ExpectedRuntimeMinutes { get; set; }
    public int? GraceRuntimeMinutes { get; set; }
    public List<Guid> DependsOn { get; set; } = new();
    public string? ConditionExpression { get; set; }
    public int? TaskSlaMinutes { get; set; }
    public int? EscalateAfterMinutes { get; set; }
    public string? EscalationUserId { get; set; }
    public string? EscalationRole { get; set; }
    public bool IsCritical { get; set; }
    public int? MaxRetries { get; set; }
    public int? RetryDelayMinutes { get; set; }
    public string? FailurePolicy { get; set; }
    public int? ApprovalAllowedDays { get; set; }
    public List<RequestTaskApprovalApproverModel> ApprovalApprovers { get; set; } = new();
}

public sealed class RequestTaskApprovalApproverModel
{
    public string Source { get; set; } = "Customer";
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
}
