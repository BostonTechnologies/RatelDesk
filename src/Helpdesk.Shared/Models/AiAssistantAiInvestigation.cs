namespace Helpdesk.Shared.Models;

public enum AiAssistantTicketArea { Incidents, Requests, Changes }
public enum AiInvestigationStatus { Queued, Started, Progress, Finding, ActionAwaitingApproval, Completed, Failed, Cancelled }
public enum AiInvestigationSource { Operator, AiAssistant, System }

public sealed class AiAssistantWebhookConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsArchived { get; set; }
    public List<AiAssistantTicketArea> TicketAreas { get; set; } = new();
    public string Endpoint { get; set; } = string.Empty;
    public string? RouteName { get; set; }
    public string? PromptTemplate { get; set; }
    public string? PermittedInputsSchemaJson { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxBodyBytes { get; set; } = 65536;
    public string? SecretReference { get; set; }
    public string? SigningSecretProtected { get; set; }
    public DateTimeOffset? LastDispatchUtc { get; set; }
    public string? LastHealthMessage { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class AiInvestigationInvocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public AiAssistantTicketArea TicketArea { get; set; }
    public Guid ConfigurationId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string OperatorAssistanceRequest { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public AiInvestigationStatus Status { get; set; } = AiInvestigationStatus.Queued;
    public string? AiAssistantRunReference { get; set; }
    public string? FailureMessage { get; set; }
    public int DispatchAttempts { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedUtc { get; set; }
}

public sealed class AiInvestigationWorklogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvocationId { get; set; }
    public string OrganizationId { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public AiAssistantTicketArea TicketArea { get; set; }
    public AiInvestigationSource Source { get; set; }
    public AiInvestigationStatus Status { get; set; }
    public string? Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public string? ArtifactReferencesJson { get; set; }
    public string? CallbackEventId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AiAssistantWebhookAuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = string.Empty;
    public Guid? ConfigurationId { get; set; }
    public Guid? InvocationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
