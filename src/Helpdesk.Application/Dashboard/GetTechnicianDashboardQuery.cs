using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Dashboard;

public record GetTechnicianDashboardQuery(
    string TechnicianId,
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
        var open = allIncidents.Count(i => i.AssignedToId == request.TechnicianId && i.State != TicketState.Resolved);
        var resolvedToday = allIncidents.Count(i => i.AssignedToId == request.TechnicianId && i.State == TicketState.Resolved && i.UpdatedAt?.Date == DateTime.UtcNow.Date);

        var allChanges = await changes.GetAllAsync();
        var pendingChanges = allChanges.Count(change =>
            change.State != TicketState.Resolved &&
            (request.IsHelpdeskAdmin ||
             (!string.IsNullOrWhiteSpace(change.OrganizationId) &&
              request.ChangeOrganizationIds.Contains(change.OrganizationId))));

        return new TechnicianDashboardDto(open, resolvedToday, pendingChanges);
    }
}
