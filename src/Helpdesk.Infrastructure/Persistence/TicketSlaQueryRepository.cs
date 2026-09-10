using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class TicketSlaQueryRepository(HelpdeskDbContext context) : ITicketSlaQueryRepository
{
    private readonly HelpdeskDbContext _context = context;

    public async Task<IReadOnlyList<TicketSlaBatchRow>> GetBatchAsync(int take, string? cursor, CancellationToken ct)
    {
        var size = Math.Max(1, take);

        var query =
            from state in _context.TicketSlaStates.AsNoTracking()
            join ticket in _context.Tickets.AsNoTracking() on state.TicketId equals ticket.Id
            where state.Status != SlaStatus.Completed
            where ticket.State != TicketState.Resolved
            select new
            {
                ticket.Id,
                ticket.OrganizationId,
                ticket.TrackingId,
                ticket.Title,
                ticket.Priority,
                ticket.ServiceId,
                ticket.State,
                Discriminator = EF.Property<string>(ticket, "Discriminator"),
                SlaState = state
            };

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            query = query.Where(x => x.Id.CompareTo(cursor) > 0);
        }

        var rows = await query
            .OrderBy(x => x.Id)
            .Take(size)
            .ToListAsync(ct);

        return rows.Select(x => new TicketSlaBatchRow(
            Cursor: x.Id,
            TicketId: x.Id,
            TenantId: x.OrganizationId,
            TicketType: ResolveTicketType(x.Discriminator),
            TicketNumber: x.TrackingId,
            Title: x.Title,
            Priority: x.Priority,
            ServiceId: x.ServiceId,
            IsClosed: x.State == TicketState.Resolved,
            SlaState: x.SlaState))
            .ToList();
    }

    private static TicketType ResolveTicketType(string? discriminator)
    {
        return discriminator switch
        {
            nameof(Request) => TicketType.Request,
            nameof(Change) => TicketType.Change,
            _ => TicketType.Incident
        };
    }
}
