using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Events;

public static class ChangeDomainEventTypes
{
    public const string Created = "DomainEvent.Change.Created";
    public const string Updated = "DomainEvent.Change.Updated";
    public const string StateChanged = "DomainEvent.Change.StateChanged";
    public const string AiReviewCompleted = "DomainEvent.Change.AiReviewCompleted";
    public const string AiReviewFailed = "DomainEvent.Change.AiReviewFailed";
    public const string AiReviewAcknowledged = "DomainEvent.Change.AiReviewAcknowledged";
}

public sealed record ChangeCreatedDomainEvent(
    string ChangeId,
    string TrackingId,
    string Title,
    string? TenantId,
    string? ChangeType,
    TicketState State,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.Created,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record ChangeUpdatedDomainEvent(
    string ChangeId,
    string TrackingId,
    string Title,
    string? TenantId,
    string? ChangeType,
    TicketState State,
    bool TemplateChanged,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.Updated,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record ChangeStateChangedDomainEvent(
    string ChangeId,
    string TrackingId,
    string? TenantId,
    TicketState PreviousState,
    TicketState CurrentState,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.StateChanged,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record ChangeAiReviewCompletedDomainEvent(
    string ChangeId,
    string TrackingId,
    string? TenantId,
    ChangeReviewGateState GateState,
    bool RequiresAcknowledgement,
    string Summary,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.AiReviewCompleted,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record ChangeAiReviewFailedDomainEvent(
    string ChangeId,
    string TrackingId,
    string? TenantId,
    string FailureReason,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.AiReviewFailed,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record ChangeAiReviewAcknowledgedDomainEvent(
    string ChangeId,
    string TrackingId,
    string? TenantId,
    string? AcknowledgedByUserId,
    string? AcknowledgedByName,
    string? Notes,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: ChangeDomainEventTypes.AiReviewAcknowledged,
        Source: "Change",
        TenantId: TenantId,
        EntityId: ChangeId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);
