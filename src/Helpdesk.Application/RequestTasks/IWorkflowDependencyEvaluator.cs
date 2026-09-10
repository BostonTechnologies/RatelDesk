namespace Helpdesk.Application.RequestTasks;

public interface IWorkflowDependencyEvaluator
{
    Task<WorkflowDependencyEvaluationResult> EvaluateAsync(string requestId, CancellationToken ct);
}

public sealed record WorkflowDependencyEvaluationResult(int UnblockedCount);
