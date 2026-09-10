namespace Helpdesk.Application.RequestTasks;

public interface IRequestTaskStateService
{
    Task<bool> EvaluateParentRequestState(string requestId, CancellationToken cancellationToken = default);
}
