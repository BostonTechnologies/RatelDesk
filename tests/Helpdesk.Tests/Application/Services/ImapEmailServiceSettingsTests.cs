using Helpdesk.Application.Services.Email;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Application.Services;

public class ImapEmailServiceSettingsTests
{
    [Fact]
    public void CurrentInboxSettingsOrdering_PrefersEnabledBackgroundSyncRecord()
    {
        var disabledOldest = CreateSettings(enabled: false, backgroundSyncEnabled: false, "disabled-oldest", DateTimeOffset.UtcNow.AddDays(-3));
        var enabledWithoutBackground = CreateSettings(enabled: true, backgroundSyncEnabled: false, "enabled-no-background", DateTimeOffset.UtcNow);
        var current = CreateSettings(enabled: true, backgroundSyncEnabled: true, "current", DateTimeOffset.UtcNow.AddDays(-1));

        var ordered = ImapEmailService.OrderByCurrentInboxSettings(new[] { disabledOldest, enabledWithoutBackground, current }.AsQueryable()).ToList();

        Assert.Equal(current.Id, ordered[0].Id);
        Assert.Equal(enabledWithoutBackground.Id, ordered[1].Id);
        Assert.Equal(disabledOldest.Id, ordered[2].Id);
    }

    [Fact]
    public void CurrentInboxSettingsOrdering_FallsBackToMostRecentlyUpdatedWhenAllDisabled()
    {
        var older = CreateSettings(enabled: false, backgroundSyncEnabled: false, "older", DateTimeOffset.UtcNow.AddDays(-2));
        var newer = CreateSettings(enabled: false, backgroundSyncEnabled: false, "newer", DateTimeOffset.UtcNow.AddDays(-1));

        var ordered = ImapEmailService.OrderByCurrentInboxSettings(new[] { older, newer }.AsQueryable()).ToList();

        Assert.Equal(newer.Id, ordered[0].Id);
    }

    [Fact]
    public void EmailInboxSettingsDisabled_DisablesImapProcessing()
    {
        var mapped = ImapEmailService.ToImapSettings(CreateSettings(enabled: false, backgroundSyncEnabled: true), requireBackgroundSync: true);

        Assert.False(mapped.Enabled);
        Assert.Equal(ImapTestStatus.Never, mapped.LastTestStatus);
    }

    [Fact]
    public void EmailInboxSettingsBackgroundSyncDisabled_DisablesImapProcessing()
    {
        var mapped = ImapEmailService.ToImapSettings(CreateSettings(enabled: true, backgroundSyncEnabled: false), requireBackgroundSync: true);

        Assert.False(mapped.Enabled);
        Assert.Equal(ImapTestStatus.Never, mapped.LastTestStatus);
    }

    [Fact]
    public void EmailInboxSettingsEnabled_MapsToImapSettings()
    {
        var mapped = ImapEmailService.ToImapSettings(CreateSettings(enabled: true, backgroundSyncEnabled: true), requireBackgroundSync: true);

        Assert.True(mapped.Enabled);
        Assert.Equal(ImapTestStatus.Success, mapped.LastTestStatus);
        Assert.Equal("outlook.office365.com", mapped.Host);
        Assert.Equal(993, mapped.Port);
        Assert.True(mapped.UseSsl);
        Assert.Equal("helpdesk@example.com", mapped.UserEmail);
        Assert.Equal("INBOX", mapped.Mailbox);
        Assert.Equal("tenant", mapped.TenantId);
        Assert.Equal("client", mapped.ClientId);
        Assert.Equal("secret", mapped.ClientSecret);
    }

    private static EmailInboxSettings CreateSettings(bool enabled, bool backgroundSyncEnabled)
        => CreateSettings(enabled, backgroundSyncEnabled, "helpdesk@example.com", DateTimeOffset.UtcNow);

    private static EmailInboxSettings CreateSettings(
        bool enabled,
        bool backgroundSyncEnabled,
        string mailboxAddress,
        DateTimeOffset updatedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            MailHost = "outlook.office365.com",
            Port = 993,
            UseSsl = true,
            MailboxAddress = mailboxAddress,
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            MailboxFolder = "INBOX",
            Enabled = enabled,
            BackgroundSyncEnabled = backgroundSyncEnabled,
            CreatedAt = updatedAt.AddMinutes(-5),
            UpdatedAt = updatedAt
        };
}
