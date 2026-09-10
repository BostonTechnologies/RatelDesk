using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Infrastructure.EmailTemplates;

public sealed class EmailLayoutResolver(
    IRepository<EmailLayout> layoutRepository,
    ITenantContext tenantContext) : IEmailLayoutResolver
{
    public async Task<EmailLayout?> ResolveAsync(int? layoutId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (layoutId.HasValue)
        {
            var match = await layoutRepository.GetAsync(layoutId.Value.ToString());
            if (match is not null)
                return match;
        }

        var all = await layoutRepository.GetAllAsync();
        var currentTenantId = ParseTenantId(tenantContext.TenantId);

        var tenantDefault = all.FirstOrDefault(x =>
            !x.IsSystem &&
            x.TenantId == currentTenantId &&
            string.Equals(x.Name, "Default", StringComparison.OrdinalIgnoreCase));
        if (tenantDefault is not null)
            return tenantDefault;

        var systemDefault = all.FirstOrDefault(x =>
            x.IsSystem &&
            string.Equals(x.Name, "Default", StringComparison.OrdinalIgnoreCase));
        if (systemDefault is not null)
            return systemDefault;

        return all.FirstOrDefault(x =>
            x.TenantId is null &&
            string.Equals(x.Name, "Default", StringComparison.OrdinalIgnoreCase));
    }

    private static int? ParseTenantId(string? tenant)
    {
        if (int.TryParse(tenant, out var parsed))
            return parsed;

        return null;
    }
}
