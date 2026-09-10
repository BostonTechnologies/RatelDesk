namespace Helpdesk.Application.Services.SupportNotifications;

public interface ISupportNotificationBootstrapper
{
    Task<SupportNotificationBootstrapResult> EnsureDefaultSupportConfigurationAsync(CancellationToken ct = default);
}

public sealed record SupportNotificationBootstrapResult(
    int OrganizationsProcessed,
    int OrganizationsSkippedMissingProvider,
    int SupportGroupsCreated,
    int CoveragesCreated,
    int SubscriptionsCreated);
