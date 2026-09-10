using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Application.Sla;

public interface ISlaPolicyRepository : IRepository<SlaPolicy>
{
    Task<SlaPolicy?> GetActiveTenantPolicyAsync(string tenantId, TicketType ticketType);
    Task<SlaPolicy?> GetActiveSystemPolicyAsync(TicketType ticketType);
    Task<List<SlaPolicy>> GetActivePoliciesAsync(string? tenantId, TicketType ticketType, CancellationToken ct = default);
    IQueryable<SlaPolicy> QueryWithEscalations();
    Task<SlaPolicy?> GetByIdWithEscalationsAsync(string id, CancellationToken ct = default);
}
