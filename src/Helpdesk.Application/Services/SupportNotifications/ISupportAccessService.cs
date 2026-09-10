namespace Helpdesk.Application.Services.SupportNotifications;

public interface ISupportAccessService
{
    Task<bool> CanUserSupportOrganizationAsync(
        string userId,
        string customerOrganizationId,
        CancellationToken ct = default);
}
