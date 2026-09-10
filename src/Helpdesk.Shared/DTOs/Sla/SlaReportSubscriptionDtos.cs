using Helpdesk.Shared.Enums;
using Helpdesk.Shared.DTOs.SlaPolicy;

namespace Helpdesk.Shared.DTOs.Sla;

public class SlaReportSubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public ReportFrequency Frequency { get; set; }
    public DayOfWeek? WeeklyDay { get; set; }
    public TimeSpan SendTimeLocal { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public List<RecipientTargetDto> Targets { get; set; } = new();
    public int LookbackDays { get; set; } = 7;
    public bool IncludeCsvAttachment { get; set; } = true;
    public bool IncludeExcelAttachment { get; set; }
    public TicketType? TicketType { get; set; }
    public string? ServiceId { get; set; }
}
