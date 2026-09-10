namespace Helpdesk.Shared.DTOs.Dashboard;

public class TechnicianDashboardDto
{
    public int OpenIncidentsCount { get; set; }
    public int ResolvedTodayCount { get; set; }
    public int PendingChangesCount { get; set; }
}
