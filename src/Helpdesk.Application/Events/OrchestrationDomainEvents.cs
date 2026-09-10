namespace Helpdesk.Application.Events;

public static class OrchestrationDomainEventTypes
{
    public const string OrchestrationSettingsUpdated = "DomainEvent.Orchestration.External orchestration.SettingsUpdated";
    public const string OrchestrationTestSucceeded = "DomainEvent.Orchestration.External orchestration.TestSucceeded";
    public const string OrchestrationTestFailed = "DomainEvent.Orchestration.External orchestration.TestFailed";
    public const string OrchestrationCallbackReceived = "DomainEvent.Orchestration.External orchestration.CallbackReceived";
    public const string OrchestrationCallbackRejected = "DomainEvent.Orchestration.External orchestration.CallbackRejected";
    public const string OrchestrationBindingCreated = "DomainEvent.Orchestration.External orchestration.BindingCreated";
    public const string OrchestrationBindingUpdated = "DomainEvent.Orchestration.External orchestration.BindingUpdated";
    public const string OrchestrationBindingDeleted = "DomainEvent.Orchestration.External orchestration.BindingDeleted";
}

public sealed record OrchestrationSettingsUpdatedEvent(
    bool Enabled,
    string? BaseUrl,
    string? Audience,
    string? TokenEndpoint,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationSettingsUpdated,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: BaseUrl,
        CorrelationId: CorrelationId,
        Reference: BaseUrl,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationTestSucceededEvent(
    int? StatusCode,
    string Message,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationTestSucceeded,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: null,
        CorrelationId: CorrelationId,
        Reference: null,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationTestFailedEvent(
    int? StatusCode,
    string Message,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationTestFailed,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: null,
        CorrelationId: CorrelationId,
        Reference: null,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationCallbackReceivedEvent(
    string RequestId,
    string? TaskId,
    string? ExecutionId,
    string? Status,
    string? CallerClientId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationCallbackReceived,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: string.IsNullOrWhiteSpace(ExecutionId) ? RequestId : ExecutionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationCallbackRejectedEvent(
    string ReasonCode,
    string? CallerClientId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationCallbackRejected,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: null,
        CorrelationId: CorrelationId,
        Reference: CallerClientId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationBindingCreatedEvent(
    string BindingId,
    string RequestFormId,
    string OrchestrationRequestDefinitionId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationBindingCreated,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: BindingId,
        CorrelationId: CorrelationId,
        Reference: OrchestrationRequestDefinitionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationBindingUpdatedEvent(
    string BindingId,
    string RequestFormId,
    string OrchestrationRequestDefinitionId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationBindingUpdated,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: BindingId,
        CorrelationId: CorrelationId,
        Reference: OrchestrationRequestDefinitionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record OrchestrationBindingDeletedEvent(
    string BindingId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: OrchestrationDomainEventTypes.OrchestrationBindingDeleted,
        Source: "Orchestration",
        TenantId: TenantId,
        EntityId: BindingId,
        CorrelationId: CorrelationId,
        Reference: BindingId,
        OccurredUtc: Timestamp.UtcDateTime);
