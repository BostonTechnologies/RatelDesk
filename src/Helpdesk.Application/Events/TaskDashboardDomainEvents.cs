namespace Helpdesk.Application.Events;

public static class TaskDashboardDomainEventTypes
{
    public const string Viewed = "DomainEvent.TaskDashboard.Viewed";
}

public sealed record TasksDashboardViewedEvent(
    string? UserId,
    bool AssignedToMe,
    string? AssignedToId,
    string? Status,
    string? Type,
    string? RequestId,
    string? ServiceId,
    string? Query,
    int ResultCount,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: TaskDashboardDomainEventTypes.Viewed,
        Source: "TaskDashboard",
        TenantId: TenantId,
        EntityId: UserId,
        CorrelationId: CorrelationId,
        Reference: Query,
        OccurredUtc: Timestamp.UtcDateTime);
