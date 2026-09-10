using Helpdesk.Application.Sla;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class SlaReportSendEventRepository(HelpdeskDbContext context) : ISlaReportSendEventRepository
{
    private readonly HelpdeskDbContext _context = context;

    public async Task<(SlaReportSendEvent Event, bool Created)> GetOrCreateAsync(SlaReportSendEvent seed, CancellationToken ct = default)
    {
        var existing = await _context.SlaReportSendEvents
            .FirstOrDefaultAsync(x =>
                x.SubscriptionId == seed.SubscriptionId &&
                x.PeriodStartUtc == seed.PeriodStartUtc &&
                x.PeriodEndUtc == seed.PeriodEndUtc, ct);
        if (existing is not null)
        {
            return (existing, false);
        }

        _context.SlaReportSendEvents.Add(seed);
        await _context.SaveChangesAsync(ct);
        return (seed, true);
    }

    public async Task UpdateAsync(SlaReportSendEvent sendEvent, CancellationToken ct = default)
    {
        _context.SlaReportSendEvents.Update(sendEvent);
        await _context.SaveChangesAsync(ct);
    }
}
