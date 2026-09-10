using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class TicketSlaEscalationEvent
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string TicketId { get; set; } = string.Empty;
    public SlaMetricType Metric { get; set; }
    public int TriggerPercent { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public string RecipientsCsv { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public EscalationSendStatus SendStatus { get; set; } = EscalationSendStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
}
