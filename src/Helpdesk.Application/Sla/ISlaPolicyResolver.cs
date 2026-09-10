using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ISlaPolicyResolver
{
    Task<SlaPolicy?> ResolveAsync(string? tenantId, TicketType ticketType, int? priority = null, string? serviceId = null);
}
