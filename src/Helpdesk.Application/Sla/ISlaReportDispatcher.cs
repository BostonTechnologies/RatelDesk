namespace Helpdesk.Application.Sla;

public interface ISlaReportDispatcher
{
    Task RunAsync(CancellationToken ct = default);
}
