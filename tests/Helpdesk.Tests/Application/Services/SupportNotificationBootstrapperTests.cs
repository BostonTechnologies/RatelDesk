using Helpdesk.Application.Services.SupportNotifications;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helpdesk.Tests.Application.Services;

public sealed class SupportNotificationBootstrapperTests
{
    [Fact]
    public async Task EnsureDefaultSupportConfiguration_CreatesProviderNamedGroupsCoverageAndSubscriptions_Idempotently()
    {
        var organizations = new InMemoryRepository<Organization>();
        var supportGroups = new InMemoryRepository<SupportGroup>();
        var coverages = new InMemoryRepository<OrganizationSupportCoverage>();
        var subscriptions = new InMemoryRepository<SupportNotificationSubscription>();
        await organizations.CreateAsync(new Organization
        {
            Id = "support-team",
            Name = "Support Team",
            IsEnabled = true
        });
        await organizations.CreateAsync(new Organization
        {
            Id = "gourmet-foods",
            Name = "GourmetFoods",
            IsEnabled = true
        });
        await organizations.CreateAsync(new Organization
        {
            Id = "msp-customer",
            Name = "MSP Customer",
            ItSupportOrganizationId = "support-team",
            IsEnabled = true
        });

        var bootstrapper = new SupportNotificationBootstrapper(
            organizations,
            supportGroups,
            coverages,
            subscriptions,
            NullLogger<SupportNotificationBootstrapper>.Instance);

        var firstRun = await bootstrapper.EnsureDefaultSupportConfigurationAsync();
        Assert.Equal(3, firstRun.OrganizationsProcessed);
        Assert.Equal(0, firstRun.OrganizationsSkippedMissingProvider);
        Assert.Equal(2, firstRun.SupportGroupsCreated);
        Assert.Equal(3, firstRun.CoveragesCreated);
        Assert.Equal(3, firstRun.SubscriptionsCreated);

        var secondRun = await bootstrapper.EnsureDefaultSupportConfigurationAsync();
        Assert.Equal(3, secondRun.OrganizationsProcessed);
        Assert.Equal(0, secondRun.OrganizationsSkippedMissingProvider);
        Assert.Equal(0, secondRun.SupportGroupsCreated);
        Assert.Equal(0, secondRun.CoveragesCreated);
        Assert.Equal(0, secondRun.SubscriptionsCreated);

        var groups = (await supportGroups.GetAllAsync()).ToList();
        Assert.Contains(groups, x =>
            x.OwningOrganizationId == "gourmet-foods" &&
            x.Name == "GourmetFoods Support");
        var supportGroup = Assert.Single(groups, x =>
            x.OwningOrganizationId == "support-team" &&
            x.Name == "Support Team Support");
        Assert.Equal(2, groups.Count);

        var coverageRows = (await coverages.GetAllAsync()).ToList();
        Assert.Contains(coverageRows, x =>
            x.CustomerOrganizationId == "gourmet-foods" &&
            x.ProviderOrganizationId == "gourmet-foods" &&
            groups.Any(g => g.Id == x.SupportGroupId && g.Name == "GourmetFoods Support") &&
            x.Role == SupportCoverageRole.Primary);
        Assert.Contains(coverageRows, x =>
            x.CustomerOrganizationId == "msp-customer" &&
            x.ProviderOrganizationId == "support-team" &&
            x.SupportGroupId == supportGroup.Id &&
            x.Role == SupportCoverageRole.Primary);
        Assert.Equal(3, coverageRows.Count);

        var subscriptionRows = (await subscriptions.GetAllAsync()).ToList();
        Assert.All(subscriptionRows, x =>
        {
            Assert.Equal(SupportNotificationEventType.TicketCreatedUnassigned, x.EventType);
            Assert.Equal(SupportNotificationRecipientType.SupportGroup, x.RecipientType);
            Assert.Equal(SupportNotificationChannel.Email, x.Channel);
            Assert.True(x.IsEnabled);
        });
        Assert.Contains(subscriptionRows, x =>
            x.CustomerOrganizationId == "msp-customer" &&
            x.RecipientId == supportGroup.Id);
        Assert.Equal(3, subscriptionRows.Count);
    }
}
