using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Dashboard;

public class AdminDashboardDto
{
    public int IncidentsToday { get; set; }
    public int UnassignedIncidents { get; set; }
    public int PendingChanges { get; set; }
    public int KnowledgeDraftCount { get; set; }
    public Dictionary<TicketState, int> IncidentCountsByState { get; set; } = new();
    public Dictionary<string, int> RequestCountsByState { get; set; } = new();
    public Dictionary<string, int> ChangeCountsByState { get; set; } = new();
}
