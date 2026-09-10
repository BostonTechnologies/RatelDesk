using Helpdesk.Application.Sla;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class TicketSlaRepository(HelpdeskDbContext context) : ITicketSlaRepository
{
    private readonly HelpdeskDbContext _context = context;

    public async Task AddAsync(TicketSlaState state)
    {
        _context.TicketSlaStates.Add(state);
        await _context.SaveChangesAsync();
    }

    public Task<TicketSlaState?> GetByTicketIdAsync(string ticketId)
    {
        return _context.TicketSlaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketId == ticketId);
    }

    public Task<TicketSlaState?> GetByTicketIdForUpdateAsync(string ticketId)
    {
        return _context.TicketSlaStates
            .FirstOrDefaultAsync(x => x.TicketId == ticketId);
    }

    public async Task UpdateAsync(TicketSlaState state)
    {
        _context.TicketSlaStates.Update(state);
        await _context.SaveChangesAsync();
    }
}
