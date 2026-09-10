namespace Helpdesk.Application.Sla;

public interface ISlaEvaluationJob
{
    Task RunAsync(CancellationToken ct);
}
