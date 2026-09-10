namespace Helpdesk.Application.Events;

public abstract record DomainEvent(
    string EventType,
    string Source,
    string? TenantId,
    string? EntityId,
    string? CorrelationId,
    string? Reference,
    DateTime OccurredUtc);
