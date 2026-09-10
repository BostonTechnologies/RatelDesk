namespace Helpdesk.Application.Events;

public sealed record TenantCreatedEvent(
    string Domain,
    string TenantId,
    string CorrelationId)
    : DomainEvent(
        EventType: "TenantCreated",
        Source: "Application",
        TenantId: TenantId,
        EntityId: TenantId,
        CorrelationId: CorrelationId,
        Reference: Domain,
        OccurredUtc: DateTime.UtcNow);
