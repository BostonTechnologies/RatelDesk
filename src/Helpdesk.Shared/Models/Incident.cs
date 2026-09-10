using System;

namespace Helpdesk.Shared.Models;

public class Incident : Ticket
{
    public ICollection<IncidentCategoryLink> CategoryLinks { get; set; } = new List<IncidentCategoryLink>();

    public string? Impact { get; set; }

    // Stores the raw HTML body of the original email.
    public string? OriginalEmailHtml { get; set; }

    // Stores the plain-text body of the original email.
    public string? OriginalEmailText { get; set; }

    // Email address of the sender of the original email.
    public string? EmailFrom { get; set; }

    // Timestamp when the original email was received (UTC).
    public DateTimeOffset? EmailReceivedUtc { get; set; }
}
