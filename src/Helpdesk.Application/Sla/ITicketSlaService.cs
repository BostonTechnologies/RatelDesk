using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ITicketSlaService
{
    Task PauseAsync(string ticketId, string userId, string reason);
    Task PauseAsync(Ticket ticket, string userId, string reason);
    Task ResumeAsync(string ticketId, string userId);
    Task ResumeAsync(Ticket ticket, string userId);
    Task AutoResumeIfDueAsync(string ticketId);
}
