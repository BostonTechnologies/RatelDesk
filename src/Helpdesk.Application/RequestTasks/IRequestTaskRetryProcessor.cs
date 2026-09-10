namespace Helpdesk.Application.RequestTasks;

public interface IRequestTaskRetryProcessor
{
    Task ProcessAsync(CancellationToken ct);
}
