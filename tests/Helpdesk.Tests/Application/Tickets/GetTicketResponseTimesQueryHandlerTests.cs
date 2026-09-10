using Helpdesk.Application.Tickets;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Xunit;

namespace Helpdesk.Tests.Application.Tickets;

public class GetTicketResponseTimesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ComputesExpectedResponseTimes()
    {
        var ticketRepo = new InMemoryRepository<Ticket>();
        var eventRepo = new InMemoryRepository<TicketEvent>();

        var ticket1 = new Incident { Title = "T1", CreatedAt = new DateTime(2025, 1, 1, 8, 0, 0) };
        var ticket2 = new Incident { Title = "T2", CreatedAt = new DateTime(2025, 1, 2, 9, 0, 0) };
        await ticketRepo.CreateAsync(ticket1);
        await ticketRepo.CreateAsync(ticket2);

        await eventRepo.CreateAsync(new TicketEvent
        {
            TicketId = ticket1.Id,
            DateTime = ticket1.CreatedAt.AddHours(1),
            Comments = "Claimed"
        });
        await eventRepo.CreateAsync(new TicketEvent
        {
            TicketId = ticket1.Id,
            DateTime = ticket1.CreatedAt.AddHours(2),
            Comments = "Ticket Closed"
        });
        await eventRepo.CreateAsync(new TicketEvent
        {
            TicketId = ticket2.Id,
            DateTime = ticket2.CreatedAt.AddHours(1),
            Comments = "Placed on hold: waiting"
        });
        await eventRepo.CreateAsync(new TicketEvent
        {
            TicketId = ticket2.Id,
            DateTime = ticket2.CreatedAt.AddHours(3),
            Comments = "Ticket Closed"
        });

        var handler = new GetTicketResponseTimesQueryHandler(ticketRepo, eventRepo);
        var query = new GetTicketResponseTimesQuery(new DateTime(2025, 1, 1), new DateTime(2025, 1, 3));
        var result = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(result);
        var resp = result.First();
        Assert.Equal(ticket1.Id, resp.TicketId);
        Assert.Equal(ticket1.CreatedAt, resp.Logged);
        Assert.Equal(ticket1.CreatedAt.AddHours(1), resp.Claimed);
        Assert.Equal(ticket1.CreatedAt.AddHours(2), resp.Closed);
    }
}
