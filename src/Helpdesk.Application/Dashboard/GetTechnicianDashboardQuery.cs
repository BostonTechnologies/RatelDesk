using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Dashboard;

public record GetTechnicianDashboardQuery(
    string TechnicianId,
    IReadOnlySet<string> IncidentOrganizationIds,
    IReadOnlySet<string> ChangeOrganizationIds,
    bool IsHelpdeskAdmin) : IRequest<TechnicianDashboardDto>;

public record TechnicianDashboardDto(int OpenIncidentsCount, int ResolvedTodayCount, int PendingChangesCount);

public class GetTechnicianDashboardQueryHandler(
    IRepository<Incident> incidents,
    IRepository<Change> changes) : IRequestHandler<GetTechnicianDashboardQuery, TechnicianDashboardDto>
{
    public async Task<TechnicianDashboardDto> Handle(GetTechnicianDashboardQuery request, CancellationToken cancellationToken)
    {
        var incidentOrganizationIds = request.IncidentOrganizationIds.ToArray();
        var changeOrganizationIds = request.ChangeOrganizationIds.ToArray();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var open = await incidents.CountAsync(incident =>
            incident.AssignedToId == request.TechnicianId &&
            (request.IsHelpdeskAdmin ||
             (incident.OrganizationId != null && incidentOrganizationIds.Contains(incident.OrganizationId))) &&
            incident.State != TicketState.Resolved,
            cancellationToken);
        var resolvedToday = await incidents.CountAsync(incident =>
            incident.AssignedToId == request.TechnicianId &&
            (request.IsHelpdeskAdmin ||
             (incident.OrganizationId != null && incidentOrganizationIds.Contains(incident.OrganizationId))) &&
            incident.State == TicketState.Resolved &&
            incident.UpdatedAt >= today && incident.UpdatedAt < tomorrow,
            cancellationToken);
        var pendingChanges = await changes.CountAsync(change =>
            change.State != TicketState.Resolved &&
            (request.IsHelpdeskAdmin ||
             (change.OrganizationId != null && changeOrganizationIds.Contains(change.OrganizationId))),
            cancellationToken);

        return new TechnicianDashboardDto(open, resolvedToday, pendingChanges);
    }
}
