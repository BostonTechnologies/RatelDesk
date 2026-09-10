using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class WorkingCalendar
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public CalendarScopeType ScopeType { get; set; } = CalendarScopeType.SystemDefault;
    public string? TenantId { get; set; }
    public string Name { get; set; } = "Default Calendar";
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public List<WorkingDayRule> WeeklyRules { get; set; } = new();
    public List<CalendarException> Exceptions { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class WorkingDayRule
{
    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public List<TimeRange> WorkingHours { get; set; } = new();
}

public class TimeRange
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

public class CalendarException
{
    public DateOnly Date { get; set; }
    public bool IsWorkingDayOverride { get; set; }
    public List<TimeRange> WorkingHoursOverride { get; set; } = new();
    public string? Note { get; set; }
}
