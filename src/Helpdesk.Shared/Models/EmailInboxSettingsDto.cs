namespace Helpdesk.Shared.Models;

public record EmailInboxSettingsDto(
    Guid Id,
    string MailHost,
    int Port,
    bool UseSsl,
    string MailboxAddress,
    string TenantId,
    string ClientId,
    string MailboxFolder,
    bool Enabled,
    bool BackgroundSyncEnabled,
    bool HasClientSecret);
