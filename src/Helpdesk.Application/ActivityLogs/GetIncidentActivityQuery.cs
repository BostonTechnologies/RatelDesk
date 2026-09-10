using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.ActivityLogs;

public record GetIncidentActivityQuery(string IncidentId) : IRequest<IEnumerable<ActivityLog>>;

public class GetIncidentActivityQueryHandler(IRepository<ActivityLog> logs) : IRequestHandler<GetIncidentActivityQuery, IEnumerable<ActivityLog>>
{
    public async Task<IEnumerable<ActivityLog>> Handle(GetIncidentActivityQuery request, CancellationToken cancellationToken)
    {
        var all = await logs.GetAllAsync();
        return all.Where(l => l.TicketId == request.IncidentId).OrderBy(l => l.Timestamp);
    }
}
