using Helpdesk.Shared.Models;

namespace Helpdesk.Application.RequestTasks;

public interface IRequestTaskLifecycleService
{
    Task<RequestTask> StartAsync(string taskId, CancellationToken ct);
    Task<RequestTask> CompleteAsync(string taskId, string? notes, CancellationToken ct);
    Task<RequestTask> FailAsync(string taskId, string reason, CancellationToken ct);
    Task<RequestTask> RetryAsync(string taskId, CancellationToken ct);
}
