using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Services;

public interface ISelfServiceAudienceService
{
    Task<bool> IsTestUserAsync(CancellationToken cancellationToken);
    Task<bool> CanAccessRequestFormAsync(RequestForm requestForm, CancellationToken cancellationToken);
    IQueryable<RequestForm> ApplyAudienceFilter(IQueryable<RequestForm> query, bool isAdmin, bool isTestUser, string? tenantId);
}

public sealed class SelfServiceAudienceService(
    HelpdeskDbContext db,
    ITenantContext tenantContext) : ISelfServiceAudienceService
{
    public async Task<bool> IsTestUserAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.IsHelpdeskAdmin)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(tenantContext.UserId))
        {
            return false;
        }

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantContext.UserId, cancellationToken);

        return user?.IsTestUser ?? false;
    }

    public async Task<bool> CanAccessRequestFormAsync(RequestForm requestForm, CancellationToken cancellationToken)
    {
        if (tenantContext.IsHelpdeskAdmin)
        {
            return true;
        }

        var isTestUser = await IsTestUserAsync(cancellationToken);
        return CanAccessRequestForm(requestForm, tenantContext.TenantId, isTestUser);
    }

    public IQueryable<RequestForm> ApplyAudienceFilter(
        IQueryable<RequestForm> query,
        bool isAdmin,
        bool isTestUser,
        string? tenantId)
    {
        if (isAdmin)
        {
            return query;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return query.Where(_ => false);
        }

        return query.Where(f =>
            (string.IsNullOrWhiteSpace(f.OrganizationId)
             || f.OrganizationId == tenantId)
            && (f.AllowedOrganizationIds == null
                || f.AllowedOrganizationIds.Count == 0
                || f.AllowedOrganizationIds.Contains(tenantId))
            && (f.ReleaseStatus == RequestFormReleaseStatus.Production
                || isTestUser));
    }

    private static bool CanAccessRequestForm(RequestForm requestForm, string? tenantId, bool isTestUser)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requestForm.OrganizationId)
            && !string.Equals(requestForm.OrganizationId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requestForm.AllowedOrganizationIds is not null
            && requestForm.AllowedOrganizationIds.Count > 0
            && !requestForm.AllowedOrganizationIds.Any(x =>
                string.Equals(x, tenantId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return requestForm.ReleaseStatus == RequestFormReleaseStatus.Production || isTestUser;
    }
}
