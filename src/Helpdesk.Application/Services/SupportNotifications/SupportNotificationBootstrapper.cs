using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.SupportNotifications;

public sealed class SupportNotificationBootstrapper(
    IRepository<Organization> organizations,
    IRepository<SupportGroup> supportGroups,
    IRepository<OrganizationSupportCoverage> coverages,
    IRepository<SupportNotificationSubscription> subscriptions,
    ILogger<SupportNotificationBootstrapper> logger) : ISupportNotificationBootstrapper
{
    public async Task<SupportNotificationBootstrapResult> EnsureDefaultSupportConfigurationAsync(CancellationToken ct = default)
    {
        _ = ct;

        var orgs = (await organizations.GetAllAsync()).Where(x => x.IsEnabled).ToList();
        var groups = (await supportGroups.GetAllAsync()).ToList();
        var coverageRows = (await coverages.GetAllAsync()).ToList();
        var subscriptionRows = (await subscriptions.GetAllAsync()).ToList();
        var organizationsProcessed = 0;
        var organizationsSkippedMissingProvider = 0;
        var supportGroupsCreated = 0;
        var coveragesCreated = 0;
        var subscriptionsCreated = 0;

        foreach (var customerOrg in orgs)
        {
            var providerOrgId = string.IsNullOrWhiteSpace(customerOrg.ItSupportOrganizationId)
                ? customerOrg.Id
                : customerOrg.ItSupportOrganizationId;
            var providerOrg = orgs.FirstOrDefault(x => string.Equals(x.Id, providerOrgId, StringComparison.OrdinalIgnoreCase));
            if (providerOrg is null)
            {
                logger.LogWarning(
                    "Skipping support notification bootstrap because provider organization was not found. CustomerOrganizationId={CustomerOrganizationId} ProviderOrganizationId={ProviderOrganizationId}",
                    customerOrg.Id,
                    providerOrgId);
                organizationsSkippedMissingProvider++;
                continue;
            }

            organizationsProcessed++;
            var defaultGroupName = BuildDefaultGroupName(providerOrg);
            var group = groups.FirstOrDefault(x =>
                string.Equals(x.OwningOrganizationId, providerOrg.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Name, defaultGroupName, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                group = await supportGroups.CreateAsync(new SupportGroup
                {
                    OwningOrganizationId = providerOrg.Id,
                    Name = defaultGroupName,
                    Description = "Default support notification routing group.",
                    IsEnabled = true
                });
                groups.Add(group);
                supportGroupsCreated++;
            }

            var coverage = coverageRows.FirstOrDefault(x =>
                string.Equals(x.CustomerOrganizationId, customerOrg.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ProviderOrganizationId, providerOrg.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.SupportGroupId, group.Id, StringComparison.OrdinalIgnoreCase) &&
                x.Role == SupportCoverageRole.Primary);
            if (coverage is null)
            {
                coverage = await coverages.CreateAsync(new OrganizationSupportCoverage
                {
                    CustomerOrganizationId = customerOrg.Id,
                    ProviderOrganizationId = providerOrg.Id,
                    SupportGroupId = group.Id,
                    Role = SupportCoverageRole.Primary,
                    IsEnabled = true
                });
                coverageRows.Add(coverage);
                coveragesCreated++;
            }

            var subscription = subscriptionRows.FirstOrDefault(x =>
                string.Equals(x.CustomerOrganizationId, customerOrg.Id, StringComparison.OrdinalIgnoreCase) &&
                x.EventType == SupportNotificationEventType.TicketCreatedUnassigned &&
                x.RecipientType == SupportNotificationRecipientType.SupportGroup &&
                string.Equals(x.RecipientId, group.Id, StringComparison.OrdinalIgnoreCase) &&
                x.Channel == SupportNotificationChannel.Email);
            if (subscription is null)
            {
                subscription = await subscriptions.CreateAsync(new SupportNotificationSubscription
                {
                    CustomerOrganizationId = customerOrg.Id,
                    EventType = SupportNotificationEventType.TicketCreatedUnassigned,
                    RecipientType = SupportNotificationRecipientType.SupportGroup,
                    RecipientId = group.Id,
                    Channel = SupportNotificationChannel.Email,
                    IsEnabled = true
                });
                subscriptionRows.Add(subscription);
                subscriptionsCreated++;
            }
        }

        return new SupportNotificationBootstrapResult(
            organizationsProcessed,
            organizationsSkippedMissingProvider,
            supportGroupsCreated,
            coveragesCreated,
            subscriptionsCreated);
    }

    private static string BuildDefaultGroupName(Organization providerOrg)
    {
        var providerName = string.IsNullOrWhiteSpace(providerOrg.Name)
            ? providerOrg.Id
            : providerOrg.Name.Trim();
        return $"{providerName} Support";
    }
}
