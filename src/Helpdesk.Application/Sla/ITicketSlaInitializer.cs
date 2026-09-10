using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ITicketSlaInitializer
{
    Task InitializeAsync(Ticket ticket);
}
