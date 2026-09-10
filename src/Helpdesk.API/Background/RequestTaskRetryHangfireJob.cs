using Hangfire;
using Helpdesk.Application.RequestTasks;

namespace Helpdesk.API.Background;

public class RequestTaskRetryHangfireJob(IRequestTaskRetryProcessor processor)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 10)]
    public Task RunAsync(CancellationToken ct)
    {
        return processor.ProcessAsync(ct);
    }
}
