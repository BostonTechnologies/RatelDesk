using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface ITenantSlaSettingsRepository
{
    Task<TenantSlaSettings?> GetByTenantIdAsync(string tenantId, CancellationToken ct = default);
}
