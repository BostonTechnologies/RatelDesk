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
        var allIncidents = await incidents.GetAllAsync();
        var authorizedIncidents = allIncidents.Where(incident =>
            incident.AssignedToId == request.TechnicianId &&
            (request.IsHelpdeskAdmin ||
             (!string.IsNullOrWhiteSpace(incident.OrganizationId) &&
              request.IncidentOrganizationIds.Contains(incident.OrganizationId))));
        var open = authorizedIncidents.Count(incident => incident.State != TicketState.Resolved);
        var resolvedToday = authorizedIncidents.Count(incident =>
            incident.State == TicketState.Resolved && incident.UpdatedAt?.Date == DateTime.UtcNow.Date);

        var allChanges = await changes.GetAllAsync();
        var pendingChanges = allChanges.Count(change =>
            change.State != TicketState.Resolved &&
            (request.IsHelpdeskAdmin ||
             (!string.IsNullOrWhiteSpace(change.OrganizationId) &&
              request.ChangeOrganizationIds.Contains(change.OrganizationId))));

        return new TechnicianDashboardDto(open, resolvedToday, pendingChanges);
    }
}
