namespace Helpdesk.Shared.Models;

using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

public class ImapEmailSettings
{
    [Key]
    public int Id { get; set; } = 1;
    public string Host { get; set; } = "Outlook.office365.com";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Mailbox { get; set; } = "INBOX";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ProcessedFolder { get; set; } = "Processed";
    public string BlockedFolder { get; set; } = "Blocked";
    public bool Enabled { get; set; } = false;
    public ImapTestStatus LastTestStatus { get; set; } = ImapTestStatus.Never; // success, failed, never
}
