using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface IBusinessTimeCalculator
{
    long GetWorkingSecondsBetween(DateTimeOffset startUtc, DateTimeOffset endUtc, WorkingCalendar calendar);
    DateTimeOffset AddWorkingSeconds(DateTimeOffset startUtc, long workingSeconds, WorkingCalendar calendar);
}
