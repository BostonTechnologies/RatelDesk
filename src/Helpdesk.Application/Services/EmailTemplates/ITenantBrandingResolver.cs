namespace Helpdesk.Application.Services.EmailTemplates;

public interface ITenantBrandingResolver
{
    Task<TenantBrandingResolved> ResolveAsync(
        string? tenantId,
        CancellationToken cancellationToken = default);
}
