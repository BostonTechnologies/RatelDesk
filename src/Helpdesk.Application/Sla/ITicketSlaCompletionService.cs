namespace Helpdesk.Application.Sla;

public interface ITicketSlaCompletionService
{
    Task HandleTicketClosedAsync(string ticketId, string closedByUserId, DateTimeOffset nowUtc);
}
