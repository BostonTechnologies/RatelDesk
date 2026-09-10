using Hangfire;
using Helpdesk.Application.Sla;

namespace Helpdesk.API.Background;

public class SlaReportDispatchHangfireJob(ISlaReportDispatcher dispatcher)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public Task RunAsync(CancellationToken ct)
    {
        return dispatcher.RunAsync(ct);
    }
}
