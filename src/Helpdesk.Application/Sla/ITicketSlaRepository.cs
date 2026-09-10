using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ITicketSlaRepository
{
    Task AddAsync(TicketSlaState state);
    Task<TicketSlaState?> GetByTicketIdAsync(string ticketId);
    Task<TicketSlaState?> GetByTicketIdForUpdateAsync(string ticketId);
    Task UpdateAsync(TicketSlaState state);
}
