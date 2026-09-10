using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaBreachEvaluator
{
    void Evaluate(TicketSlaState state);
}
