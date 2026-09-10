using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Sla;

public class WorkingCalendarDto
{
    public string Id { get; set; } = string.Empty;
    public CalendarScopeType ScopeType { get; set; }
    public string? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public List<WorkingDayRuleDto> WeeklyRules { get; set; } = new();
    public List<CalendarExceptionDto> Exceptions { get; set; } = new();
    public bool IsActive { get; set; }
}

public class WorkingDayRuleDto
{
    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public List<TimeRangeDto> WorkingHours { get; set; } = new();
}

public class TimeRangeDto
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

public class CalendarExceptionDto
{
    public DateOnly Date { get; set; }
    public bool IsWorkingDayOverride { get; set; }
    public List<TimeRangeDto> WorkingHoursOverride { get; set; } = new();
    public string? Note { get; set; }
}

public class TenantSlaSettingsDto
{
    public string TenantId { get; set; } = string.Empty;
    public bool UseBusinessHours { get; set; }
    public string? CalendarId { get; set; }
}
