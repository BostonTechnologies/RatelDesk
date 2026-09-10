using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaEmailTemplate
{
    EmailMessage BuildWarning(Ticket ticket, SlaClockSnapshot snapshot, SlaEscalationRule rule, List<string> recipients);
}
