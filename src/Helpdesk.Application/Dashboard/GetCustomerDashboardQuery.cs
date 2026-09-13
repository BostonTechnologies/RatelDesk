using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Dashboard;

public record GetCustomerDashboardQuery(string CustomerId, IReadOnlySet<string>? IncidentOrganizationIds = null) : IRequest<CustomerDashboardDto>;

public record CustomerDashboardDto(int OpenTicketsCount, int ResolvedTicketsCount);

public class GetCustomerDashboardQueryHandler(IRepository<Incident> incidents) : IRequestHandler<GetCustomerDashboardQuery, CustomerDashboardDto>
{
    public async Task<CustomerDashboardDto> Handle(GetCustomerDashboardQuery request, CancellationToken cancellationToken)
    {
        var allIncidents = await incidents.GetAllAsync();
        var customerIncidents = allIncidents.Where(i => i.CustomerId == request.CustomerId &&
            (request.IncidentOrganizationIds is null ||
             i.OrganizationId is not null && request.IncidentOrganizationIds.Contains(i.OrganizationId))).ToList();
        var open = customerIncidents.Count(i => i.State != TicketState.Resolved);
        var resolved = customerIncidents.Count(i => i.State == TicketState.Resolved);
        return new CustomerDashboardDto(open, resolved);
    }
}
