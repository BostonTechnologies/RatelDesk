using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class InboundEmailProcessingLog
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string MessageId { get; set; } = string.Empty;
    public Guid? MailboxId { get; set; }
    public string MailboxKey { get; set; } = "default";
    public string? TenantId { get; set; }
    public string? RuleId { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public InboundEmailProcessingStatus Status { get; set; }
    public string? TicketId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
