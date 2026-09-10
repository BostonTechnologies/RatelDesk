using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaReportSendEventRepository
{
    Task<(SlaReportSendEvent Event, bool Created)> GetOrCreateAsync(SlaReportSendEvent seed, CancellationToken ct = default);
    Task UpdateAsync(SlaReportSendEvent sendEvent, CancellationToken ct = default);
}
