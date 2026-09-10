using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Helpdesk.Infrastructure.Persistence;

public class TicketSlaEscalationEventRepository(HelpdeskDbContext context) : ITicketSlaEscalationEventRepository
{
    private readonly HelpdeskDbContext _context = context;

    public Task<TicketSlaEscalationEvent?> GetAsync(string ticketId, SlaMetricType metric, int triggerPercent, CancellationToken ct = default)
    {
        return _context.TicketSlaEscalationEvents
            .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.Metric == metric && x.TriggerPercent == triggerPercent, ct);
    }

    public async Task<(TicketSlaEscalationEvent Event, bool Created)> GetOrCreateAsync(TicketSlaEscalationEvent seed, CancellationToken ct = default)
    {
        var existing = await GetAsync(seed.TicketId, seed.Metric, seed.TriggerPercent, ct);
        if (existing is not null)
        {
            return (existing, false);
        }

        try
        {
            _context.TicketSlaEscalationEvents.Add(seed);
            await _context.SaveChangesAsync(ct);
            return (seed, true);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var createdByOtherWorker = await GetAsync(seed.TicketId, seed.Metric, seed.TriggerPercent, ct)
                ?? throw new InvalidOperationException("Escalation event was created concurrently but could not be reloaded.");
            return (createdByOtherWorker, false);
        }
    }

    public async Task UpdateAsync(TicketSlaEscalationEvent escalationEvent, CancellationToken ct = default)
    {
        _context.TicketSlaEscalationEvents.Update(escalationEvent);
        await _context.SaveChangesAsync(ct);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
