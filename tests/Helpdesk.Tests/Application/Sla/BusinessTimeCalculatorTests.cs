using Helpdesk.Application.Sla;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Application.Sla;

public class BusinessTimeCalculatorTests
{
    [Fact]
    public void AddWorkingSeconds_FridayLate_DueOnMondayMorning()
    {
        var calc = new BusinessTimeCalculator();
        var calendar = BuildWeekdayCalendar();
        var startUtc = new DateTimeOffset(2026, 2, 20, 14, 0, 0, TimeSpan.Zero); // Friday 16:00 in Africa/Johannesburg

        var due = calc.AddWorkingSeconds(startUtc, 2 * 3600, calendar);

        Assert.Equal(new DateTimeOffset(2026, 2, 23, 7, 0, 0, TimeSpan.Zero), due); // Monday 09:00 local
    }

    [Fact]
    public void GetWorkingSecondsBetween_ExcludesWeekendAndHolidays()
    {
        var calc = new BusinessTimeCalculator();
        var calendar = BuildWeekdayCalendar();
        calendar.Exceptions.Add(new CalendarException
        {
            Date = new DateOnly(2026, 2, 23),
            IsWorkingDayOverride = false,
            Note = "Holiday"
        });

        var startUtc = new DateTimeOffset(2026, 2, 20, 6, 0, 0, TimeSpan.Zero); // Fri 08:00 local
        var endUtc = new DateTimeOffset(2026, 2, 24, 15, 0, 0, TimeSpan.Zero); // Tue 17:00 local

        var seconds = calc.GetWorkingSecondsBetween(startUtc, endUtc, calendar);

        Assert.Equal(2 * 9 * 3600, seconds); // Fri + Tue only
    }

    private static WorkingCalendar BuildWeekdayCalendar()
    {
        var rules = Enumerable.Range(0, 7)
            .Select(day => new WorkingDayRule
            {
                DayOfWeek = day,
                IsWorkingDay = day is >= 1 and <= 5,
                WorkingHours = day is >= 1 and <= 5
                    ? new List<TimeRange> { new() { Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(17) } }
                    : new List<TimeRange>()
            })
            .ToList();

        return new WorkingCalendar
        {
            TimeZoneId = "Africa/Johannesburg",
            WeeklyRules = rules
        };
    }
}
