using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public class SlaBreachEvaluator : ISlaBreachEvaluator
{
    public void Evaluate(TicketSlaState state)
    {
        var now = DateTimeOffset.UtcNow;

        if (!state.ResponseBreached && now > state.ResponseDueAt)
        {
            state.ResponseBreached = true;
        }

        if (!state.ResolutionBreached && now > state.ResolutionDueAt)
        {
            state.ResolutionBreached = true;
            state.Status = SlaStatus.Breached;
        }
    }
}
