namespace Helpdesk.Application.Sla;

public interface ITicketSlaService
{
    Task PauseAsync(string ticketId, string userId, string reason);
    Task ResumeAsync(string ticketId, string userId);
    Task AutoResumeIfDueAsync(string ticketId);
}
