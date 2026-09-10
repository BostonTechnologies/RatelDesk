using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaEscalationEvaluator
{
    Task EvaluateAndNotifyAsync(Ticket ticket, TicketSlaState slaState, DateTimeOffset nowUtc, CancellationToken ct = default);
}
