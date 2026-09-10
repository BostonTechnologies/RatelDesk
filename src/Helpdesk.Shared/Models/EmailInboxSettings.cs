namespace Helpdesk.Shared.Models;

public class EmailInboxSettings
{
    public Guid Id { get; set; }
    public string MailHost { get; set; } = default!;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string MailboxAddress { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string MailboxFolder { get; set; } = "INBOX";
    public bool Enabled { get; set; } = false;
    public bool BackgroundSyncEnabled { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
