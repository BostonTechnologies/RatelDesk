using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Tickets;

public record MarkTicketAsSeenCommand(string TicketId) : IRequest<bool>;

/// <summary>
/// Handles the command to mark a ticket and its associated work logs as seen by the customer.
/// </summary>
/// <remarks>This handler updates the <see cref="Ticket.LastViewedByCustomerAt"/> property of the specified ticket
/// and sets the <see cref="WorkLog.SeenByCustomerAt"/> property for all unread work logs associated with the
/// ticket.</remarks>
/// <param name="ticketRepo"></param>
/// <param name="worklogRepo"></param>
public class MarkTicketAsSeenCommandHandler(
    IRepository<Ticket> ticketRepo,
    IRepository<WorkLog> worklogRepo) : IRequestHandler<MarkTicketAsSeenCommand, bool>
{
    public async Task<bool> Handle(MarkTicketAsSeenCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepo.GetAsync(request.TicketId);
        if (ticket is null) return false;

        ticket.LastViewedByCustomerAt = DateTime.UtcNow;
        await ticketRepo.UpdateAsync(ticket);

        var unreadWorklogs = (await worklogRepo.GetAllAsync())
            .Where(w => w.TicketId == request.TicketId && w.SeenByCustomerAt == null);

        foreach (var worklog in unreadWorklogs)
        {
            worklog.SeenByCustomerAt = DateTime.UtcNow;
            await worklogRepo.UpdateAsync(worklog);
        }

        return true;
    }
}
