using Helpdesk.Application.Sla;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class TenantSlaSettingsRepository(HelpdeskDbContext context) : ITenantSlaSettingsRepository
{
    private readonly HelpdeskDbContext _context = context;

    public Task<TenantSlaSettings?> GetByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        return _context.TenantSlaSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
    }
}
