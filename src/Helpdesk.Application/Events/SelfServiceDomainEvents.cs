namespace Helpdesk.Application.Events;

public static class SelfServiceDomainEventTypes
{
    public const string RequestSubmitted = "DomainEvent.SelfService.Request.Submitted";
    public const string RequestsViewed = "DomainEvent.SelfService.Requests.Viewed";
    public const string RequestOpened = "DomainEvent.SelfService.Request.Opened";
}

public sealed record SelfServiceRequestSubmittedEvent(
    string RequestId,
    string RequestFormId,
    string ServiceId,
    string TrackingId,
    string? SubmittedByUserId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: SelfServiceDomainEventTypes.RequestSubmitted,
        Source: "SelfService",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record MyRequestsViewedEvent(
    string? UserId,
    int Page,
    int PageSize,
    string? Query,
    int ResultCount,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: SelfServiceDomainEventTypes.RequestsViewed,
        Source: "SelfService",
        TenantId: TenantId,
        EntityId: UserId,
        CorrelationId: CorrelationId,
        Reference: Query,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record MyRequestOpenedEvent(
    string? UserId,
    string RequestId,
    string TrackingId,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: SelfServiceDomainEventTypes.RequestOpened,
        Source: "SelfService",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: TrackingId,
        OccurredUtc: Timestamp.UtcDateTime);
