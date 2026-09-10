namespace Helpdesk.Application.Events;

public static class RequestFormDomainEventTypes
{
    public const string Created = "DomainEvent.RequestForm.Created";
    public const string Updated = "DomainEvent.RequestForm.Updated";
}

public sealed record RequestFormCreatedDomainEvent(
    string RequestFormId,
    string ServiceId,
    string Title,
    int TaskCount,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: RequestFormDomainEventTypes.Created,
        Source: "RequestForm",
        TenantId: TenantId,
        EntityId: RequestFormId,
        CorrelationId: CorrelationId,
        Reference: RequestFormId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record RequestFormUpdatedDomainEvent(
    string RequestFormId,
    string ServiceId,
    string Title,
    int TaskCount,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: RequestFormDomainEventTypes.Updated,
        Source: "RequestForm",
        TenantId: TenantId,
        EntityId: RequestFormId,
        CorrelationId: CorrelationId,
        Reference: RequestFormId,
        OccurredUtc: Timestamp.UtcDateTime);
