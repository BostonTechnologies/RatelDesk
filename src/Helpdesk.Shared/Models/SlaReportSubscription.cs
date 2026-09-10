using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SlaReportSubscription
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;
    public DayOfWeek? WeeklyDay { get; set; }
    public TimeSpan SendTimeLocal { get; set; } = TimeSpan.FromHours(8);
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public List<RecipientTarget> Targets { get; set; } = new();
    public int LookbackDays { get; set; } = 7;
    public bool IncludeCsvAttachment { get; set; } = true;
    public bool IncludeExcelAttachment { get; set; }
    public TicketType? TicketType { get; set; }
    public string? ServiceId { get; set; }
}
