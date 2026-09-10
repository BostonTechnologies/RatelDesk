using Hangfire;
using Helpdesk.Application.Sla;

namespace Helpdesk.API.Background;

public class SlaEvaluationHangfireJob(ISlaEvaluationJob job)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public Task RunAsync(CancellationToken ct)
    {
        return job.RunAsync(ct);
    }
}
