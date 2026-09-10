namespace Helpdesk.Application.Services.Tickets;

public interface ITicketRefGeneratorService
{
    Task<string> NextReferenceAsync(string prefix);
}
