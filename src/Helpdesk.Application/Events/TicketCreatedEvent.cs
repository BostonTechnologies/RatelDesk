namespace Helpdesk.Application.Events;

public sealed record TicketCreatedEvent(
    string TrackingId,
    string TicketId,
    string? TenantId,
    string? RequesterEmail,
    string CorrelationId)
    : DomainEvent(
        EventType: "TicketCreated",
        Source: "Application",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: DateTime.UtcNow);
