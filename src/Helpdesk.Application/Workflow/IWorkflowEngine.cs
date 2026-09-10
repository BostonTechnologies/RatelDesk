namespace Helpdesk.Application.Workflow;

public interface IWorkflowEngine
{
    Task<WorkflowRunResult> RunAsync(string requestId, WorkflowRunReason reason, CancellationToken ct);
}

public enum WorkflowRunReason
{
    RequestCreated = 1,
    TaskCompleted = 2,
    TaskFailed = 3,
    TaskSkipped = 4,
    TaskRetried = 5,
    AutomationCallback = 6,
    ManualTrigger = 7,
    ApprovalReceived = 8,
    ApprovalRejected = 9,
    ApprovalExpired = 10
}

public sealed class WorkflowRunResult
{
    public string RequestId { get; init; } = string.Empty;
    public int UnblockedCount { get; init; }
    public int AutoStartedCount { get; init; }
    public bool ParentStateChanged { get; init; }

    public static WorkflowRunResult Failed(string requestId) => new()
    {
        RequestId = requestId
    };
}
