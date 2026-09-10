namespace Helpdesk.Application.Events;

public sealed record CustomerCreatedEvent(
    string Email,
    string CustomerId,
    string TenantId,
    string CorrelationId)
    : DomainEvent(
        EventType: "CustomerCreated",
        Source: "Application",
        TenantId: TenantId,
        EntityId: CustomerId,
        CorrelationId: CorrelationId,
        Reference: Email,
        OccurredUtc: DateTime.UtcNow);
