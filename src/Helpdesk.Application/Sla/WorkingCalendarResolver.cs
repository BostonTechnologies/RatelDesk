namespace Helpdesk.Application.Sla;

public class WorkingCalendarResolver(
    IWorkingCalendarRepository calendars,
    ITenantSlaSettingsRepository settings) : IWorkingCalendarResolver
{
    public async Task<Helpdesk.Shared.Models.WorkingCalendar?> ResolveAsync(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var tenantSettings = await settings.GetByTenantIdAsync(tenantId);
            if (!string.IsNullOrWhiteSpace(tenantSettings?.CalendarId))
            {
                var explicitCalendar = await calendars.GetActiveByIdAsync(tenantSettings.CalendarId);
                if (explicitCalendar is not null)
                {
                    return explicitCalendar;
                }
            }

            var tenantCalendar = await calendars.GetActiveTenantCalendarAsync(tenantId);
            if (tenantCalendar is not null)
            {
                return tenantCalendar;
            }
        }

        return await calendars.GetActiveSystemCalendarAsync();
    }
}
