namespace Helpdesk.Application.Workflow;

public interface IFailurePolicyEngine
{
    Task<FailurePolicyResult> OnTaskFailedAsync(string requestId, string taskId, CancellationToken ct);
}

public sealed class FailurePolicyResult
{
    public bool RequestStateChanged { get; init; }
    public bool RetryScheduled { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public string Decision { get; init; } = string.Empty;
}
