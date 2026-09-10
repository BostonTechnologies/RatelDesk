namespace Helpdesk.Application.RequestTasks;

public interface ITaskEscalationProcessor
{
    Task ProcessAsync(CancellationToken ct);
}
