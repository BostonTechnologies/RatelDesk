using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public class BusinessTimeCalculator : IBusinessTimeCalculator
{
    public long GetWorkingSecondsBetween(DateTimeOffset startUtc, DateTimeOffset endUtc, WorkingCalendar calendar)
    {
        if (endUtc <= startUtc)
        {
            return 0;
        }

        var timeZone = ResolveTimeZone(calendar.TimeZoneId);
        var startLocal = TimeZoneInfo.ConvertTime(startUtc, timeZone);
        var endLocal = TimeZoneInfo.ConvertTime(endUtc, timeZone);

        var totalSeconds = 0d;
        var day = DateOnly.FromDateTime(startLocal.Date);
        var endDay = DateOnly.FromDateTime(endLocal.Date);
        while (day <= endDay)
        {
            var ranges = GetRangesForDay(calendar, day);
            if (ranges.Count > 0)
            {
                var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), startLocal.Offset);
                var dayEnd = dayStart.AddDays(1);
                var effectiveStart = Max(startLocal, dayStart);
                var effectiveEnd = Min(endLocal, dayEnd);

                if (effectiveEnd > effectiveStart)
                {
                    foreach (var range in ranges)
                    {
                        var rangeStart = dayStart.Add(range.Start);
                        var rangeEnd = dayStart.Add(range.End);
                        var overlapStart = Max(effectiveStart, rangeStart);
                        var overlapEnd = Min(effectiveEnd, rangeEnd);
                        if (overlapEnd > overlapStart)
                        {
                            totalSeconds += (overlapEnd - overlapStart).TotalSeconds;
                        }
                    }
                }
            }

            day = day.AddDays(1);
        }

        return (long)Math.Floor(totalSeconds);
    }

    public DateTimeOffset AddWorkingSeconds(DateTimeOffset startUtc, long workingSeconds, WorkingCalendar calendar)
    {
        if (workingSeconds <= 0)
        {
            return startUtc;
        }

        var timeZone = ResolveTimeZone(calendar.TimeZoneId);
        var cursor = TimeZoneInfo.ConvertTime(startUtc, timeZone);
        var remaining = workingSeconds;
        var guard = 0;

        while (remaining > 0)
        {
            guard++;
            if (guard > 3660)
            {
                break;
            }

            var day = DateOnly.FromDateTime(cursor.Date);
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), cursor.Offset);
            var ranges = GetRangesForDay(calendar, day);
            if (ranges.Count == 0)
            {
                cursor = dayStart.AddDays(1);
                continue;
            }

            var advancedInDay = false;
            foreach (var range in ranges)
            {
                var rangeStart = dayStart.Add(range.Start);
                var rangeEnd = dayStart.Add(range.End);

                if (cursor >= rangeEnd)
                {
                    continue;
                }

                var effectiveStart = cursor <= rangeStart ? rangeStart : cursor;
                var available = (long)Math.Floor((rangeEnd - effectiveStart).TotalSeconds);
                if (available <= 0)
                {
                    continue;
                }

                if (remaining <= available)
                {
                    var resultLocal = effectiveStart.AddSeconds(remaining);
                    return TimeZoneInfo.ConvertTime(resultLocal, TimeZoneInfo.Utc);
                }

                remaining -= available;
                cursor = rangeEnd;
                advancedInDay = true;
            }

            if (!advancedInDay)
            {
                cursor = dayStart.AddDays(1);
            }
        }

        return TimeZoneInfo.ConvertTime(cursor, TimeZoneInfo.Utc);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static List<TimeRange> GetRangesForDay(WorkingCalendar calendar, DateOnly day)
    {
        var exception = calendar.Exceptions.FirstOrDefault(x => x.Date == day);
        if (exception is not null)
        {
            return exception.IsWorkingDayOverride
                ? NormalizeRanges(exception.WorkingHoursOverride)
                : new List<TimeRange>();
        }

        var weekday = (int)day.DayOfWeek;
        var rule = calendar.WeeklyRules.FirstOrDefault(x => x.DayOfWeek == weekday);
        if (rule is null || !rule.IsWorkingDay)
        {
            return new List<TimeRange>();
        }

        return NormalizeRanges(rule.WorkingHours);
    }

    private static List<TimeRange> NormalizeRanges(List<TimeRange> ranges)
    {
        return ranges
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .Select(x => new TimeRange { Start = x.Start, End = x.End })
            .ToList();
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b)
    {
        return a >= b ? a : b;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b)
    {
        return a <= b ? a : b;
    }
}
