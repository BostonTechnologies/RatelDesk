namespace Helpdesk.Infrastructure.Configuration;

/// <summary>
/// Represents configuration options for connecting to an Exchange email service.
/// </summary>
/// <remarks>This class is used to store the necessary credentials and mailbox information required to
/// authenticate and interact with an Exchange email service. Ensure that all properties are set with valid values
/// before using these options in an email client or service.</remarks>
public sealed class ExchangeEmailOptions
{
    public bool Enabled { get; set; }
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string MailboxAddress { get; set; } = "";
}
