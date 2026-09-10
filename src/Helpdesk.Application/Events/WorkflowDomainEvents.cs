using Helpdesk.Application.Workflow;

namespace Helpdesk.Application.Events;

public static class WorkflowDomainEventTypes
{
    public const string Evaluated = "DomainEvent.Workflow.Evaluated";
    public const string Progressed = "DomainEvent.Workflow.Progressed";
    public const string RequestFailedByCriticalTask = "DomainEvent.Workflow.RequestFailedByCriticalTask";
    public const string RequestBlockedByTaskFailure = "DomainEvent.Workflow.RequestBlockedByTaskFailure";
    public const string TaskFailureIgnored = "DomainEvent.Workflow.TaskFailureIgnored";
}

public sealed record WorkflowEvaluatedEvent(
    string RequestId,
    WorkflowRunReason Reason,
    int UnblockedCount,
    int AutoStartedCount,
    bool ParentStateChanged,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: WorkflowDomainEventTypes.Evaluated,
        Source: "Workflow",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: RequestId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkflowProgressedEvent(
    string RequestId,
    WorkflowRunReason Reason,
    int UnblockedCount,
    int AutoStartedCount,
    bool ParentStateChanged,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: WorkflowDomainEventTypes.Progressed,
        Source: "Workflow",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: RequestId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkflowRequestFailedByCriticalTaskEvent(
    string RequestId,
    string TaskId,
    string TaskName,
    bool IsCritical,
    string FailurePolicy,
    int RetryCount,
    DateTimeOffset? NextRetryAt,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: WorkflowDomainEventTypes.RequestFailedByCriticalTask,
        Source: "Workflow",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: TaskId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkflowRequestBlockedByTaskFailureEvent(
    string RequestId,
    string TaskId,
    string TaskName,
    bool IsCritical,
    string FailurePolicy,
    int RetryCount,
    DateTimeOffset? NextRetryAt,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: WorkflowDomainEventTypes.RequestBlockedByTaskFailure,
        Source: "Workflow",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: TaskId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkflowTaskFailureIgnoredEvent(
    string RequestId,
    string TaskId,
    string TaskName,
    bool IsCritical,
    string FailurePolicy,
    int RetryCount,
    DateTimeOffset? NextRetryAt,
    string? TenantId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: WorkflowDomainEventTypes.TaskFailureIgnored,
        Source: "Workflow",
        TenantId: TenantId,
        EntityId: RequestId,
        CorrelationId: CorrelationId,
        Reference: TaskId,
        OccurredUtc: Timestamp.UtcDateTime);
