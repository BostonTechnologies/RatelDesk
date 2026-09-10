namespace Helpdesk.Application.Services.EmailTemplates;

public interface ITenantBrandAssetStorageService
{
    Task<string> SaveLogoAsync(
        int tenantId,
        string filename,
        byte[] content);
}
