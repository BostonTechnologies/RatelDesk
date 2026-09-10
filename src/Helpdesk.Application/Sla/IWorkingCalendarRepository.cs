using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public interface IWorkingCalendarRepository
{
    Task<WorkingCalendar?> GetActiveTenantCalendarAsync(string tenantId, CancellationToken ct = default);
    Task<WorkingCalendar?> GetActiveSystemCalendarAsync(CancellationToken ct = default);
    Task<WorkingCalendar?> GetActiveByIdAsync(string calendarId, CancellationToken ct = default);
}
