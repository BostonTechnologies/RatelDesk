using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public sealed class SlaPolicyRepository(HelpdeskDbContext context)
    : EfRepository<SlaPolicy>(context), ISlaPolicyRepository
{
    private readonly HelpdeskDbContext _context = context;

    public IQueryable<SlaPolicy> QueryWithEscalations()
    {
        return _context.SlaPolicies
            .AsNoTracking()
            .Include(x => x.Escalations);
    }

    public Task<SlaPolicy?> GetByIdWithEscalationsAsync(string id, CancellationToken ct = default)
    {
        return _context.SlaPolicies
            .Include(x => x.Escalations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<SlaPolicy?> GetActiveTenantPolicyAsync(string tenantId, TicketType ticketType)
    {
        return _context.SlaPolicies
            .AsNoTracking()
            .Include(policy => policy.Escalations)
            .Where(policy =>
                policy.ScopeType == SlaScopeType.Tenant &&
                policy.IsActive &&
                policy.TenantId == tenantId &&
                policy.AppliesTo == ticketType)
            .FirstOrDefaultAsync();
    }

    public Task<SlaPolicy?> GetActiveSystemPolicyAsync(TicketType ticketType)
    {
        return _context.SlaPolicies
            .AsNoTracking()
            .Include(policy => policy.Escalations)
            .Where(policy =>
                policy.ScopeType == SlaScopeType.SystemDefault &&
                policy.IsActive &&
                policy.AppliesTo == ticketType)
            .FirstOrDefaultAsync();
    }

    public Task<List<SlaPolicy>> GetActivePoliciesAsync(string? tenantId, TicketType ticketType, CancellationToken ct = default)
    {
        return _context.SlaPolicies
            .AsNoTracking()
            .Include(policy => policy.Escalations)
            .Where(policy =>
                policy.IsActive &&
                policy.AppliesTo == ticketType &&
                ((policy.ScopeType == SlaScopeType.Tenant && !string.IsNullOrWhiteSpace(tenantId) && policy.TenantId == tenantId) ||
                 policy.ScopeType == SlaScopeType.SystemDefault))
            .ToListAsync(ct);
    }
}
