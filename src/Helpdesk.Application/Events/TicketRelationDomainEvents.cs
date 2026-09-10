using Helpdesk.Shared.Enums;

namespace Helpdesk.Application.Events;

public sealed record TicketRelationCreatedDomainEvent(
    string SourceTicketId,
    string SourceTrackingId,
    string TargetTicketId,
    string TargetTrackingId,
    TicketRelationType RelationType,
    bool SourceClosed,
    int AddedParentListenerCount,
    string? TenantId,
    string CorrelationId)
    : DomainEvent(
        EventType: "TicketRelationCreated",
        Source: "Application",
        TenantId: TenantId,
        EntityId: SourceTicketId,
        CorrelationId: CorrelationId,
        Reference: $"{SourceTrackingId}->{TargetTrackingId}",
        OccurredUtc: DateTime.UtcNow);
