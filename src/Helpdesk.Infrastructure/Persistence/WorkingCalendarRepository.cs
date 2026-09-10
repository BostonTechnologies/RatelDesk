using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class WorkingCalendarRepository(HelpdeskDbContext context) : IWorkingCalendarRepository
{
    private readonly HelpdeskDbContext _context = context;

    public Task<WorkingCalendar?> GetActiveTenantCalendarAsync(string tenantId, CancellationToken ct = default)
    {
        return _context.WorkingCalendars
            .AsNoTracking()
            .Where(x => x.ScopeType == CalendarScopeType.Tenant && x.IsActive && x.TenantId == tenantId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<WorkingCalendar?> GetActiveSystemCalendarAsync(CancellationToken ct = default)
    {
        return _context.WorkingCalendars
            .AsNoTracking()
            .Where(x => x.ScopeType == CalendarScopeType.SystemDefault && x.IsActive)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<WorkingCalendar?> GetActiveByIdAsync(string calendarId, CancellationToken ct = default)
    {
        return _context.WorkingCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == calendarId && x.IsActive, ct);
    }
}
