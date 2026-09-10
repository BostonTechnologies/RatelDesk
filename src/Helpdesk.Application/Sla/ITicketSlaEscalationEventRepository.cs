using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ITicketSlaEscalationEventRepository
{
    Task<TicketSlaEscalationEvent?> GetAsync(string ticketId, SlaMetricType metric, int triggerPercent, CancellationToken ct = default);
    Task<(TicketSlaEscalationEvent Event, bool Created)> GetOrCreateAsync(TicketSlaEscalationEvent seed, CancellationToken ct = default);
    Task UpdateAsync(TicketSlaEscalationEvent escalationEvent, CancellationToken ct = default);
}
