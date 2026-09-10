using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SlaReportSendEvent
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string SubscriptionId { get; set; } = string.Empty;
    public DateTimeOffset PeriodStartUtc { get; set; }
    public DateTimeOffset PeriodEndUtc { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
    public ReportSendStatus Status { get; set; } = ReportSendStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
