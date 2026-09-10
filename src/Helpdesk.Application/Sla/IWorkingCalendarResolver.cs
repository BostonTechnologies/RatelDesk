using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface IWorkingCalendarResolver
{
    Task<WorkingCalendar?> ResolveAsync(string? tenantId);
}
