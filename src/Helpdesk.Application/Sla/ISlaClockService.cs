using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaClockService
{
    SlaClockSnapshot Compute(TicketSlaState state, DateTimeOffset nowUtc, WorkingCalendar? calendar = null);
}
