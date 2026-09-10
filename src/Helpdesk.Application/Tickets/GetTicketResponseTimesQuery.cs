using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Messaging;
using Dodo.Primitives;

namespace Helpdesk.Application.Tickets;

public record GetTicketResponseTimesQuery(DateTime Start, DateTime End) : IRequest<IEnumerable<TicketResponseTimeDto>>;

public record TicketResponseTimeDto(string TicketId, DateTime Logged, DateTime? Claimed, DateTime? Closed);

/// <summary>
/// Handles the query to retrieve response times for tickets within a specified date range.
/// </summary>
/// <remarks>This handler processes the <see cref="GetTicketResponseTimesQuery"/> to calculate response times for
/// tickets. It retrieves tickets and their associated events within the specified date range, determines when each
/// ticket was claimed and closed, and returns a collection of <see cref="TicketResponseTimeDto"/> objects containing
/// the relevant response time data.</remarks>
/// <param name="tickets">The repository for accessing ticket data.</param>
/// <param name="events">The repository for accessing ticket event data.</param>
public class GetTicketResponseTimesQueryHandler(IRepository<Ticket> tickets, IRepository<TicketEvent> events)
    : IRequestHandler<GetTicketResponseTimesQuery, IEnumerable<TicketResponseTimeDto>>
{
    public async Task<IEnumerable<TicketResponseTimeDto>> Handle(GetTicketResponseTimesQuery request, CancellationToken cancellationToken)
    {
        var ticketList = (await tickets.GetAllAsync()).Where(t => t.CreatedAt >= request.Start && t.CreatedAt <= request.End).ToList();
        var eventList = (await events.GetAllAsync())
            .Where(e => e.DateTime >= request.Start && e.DateTime <= request.End)
            .OrderBy(e => e.DateTime)
            .ToList();
        var result = new List<TicketResponseTimeDto>();

        foreach (var ticket in ticketList)
        {
            var claimed = eventList.FirstOrDefault(e => e.TicketId == ticket.Id);
            var closed = eventList.LastOrDefault(e => e.TicketId == ticket.Id && e.Comments.Contains("Ticket Closed"));

            if (claimed is not null && !claimed.Comments.Contains("Placed on hold:"))
            {
                result.Add(new TicketResponseTimeDto(ticket.Id, ticket.CreatedAt, claimed.DateTime, closed?.DateTime));
            }
        }

        return result;
    }
}
