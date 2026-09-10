using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class TicketSlaCompletionServiceTests
{
    [Fact]
    public async Task HandleTicketClosedAsync_SetsCompletionSnapshot_WhenClosedWithinSla()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();

        var now = DateTimeOffset.UtcNow;
        var ticket = new Incident { Id = "t1", State = TicketState.Resolved };
        var state = new TicketSlaState
        {
            TicketId = "t1",
            StartedAt = now.AddMinutes(-30),
            ResponseDueAt = now.AddHours(1),
            ResolutionDueAt = now.AddHours(2),
            Status = SlaStatus.InProgress
        };

        tickets.GetAsync("t1").Returns(ticket);
        states.GetByTicketIdForUpdateAsync("t1").Returns(state);

        var sut = new TicketSlaCompletionService(tickets, states, clock);

        await sut.HandleTicketClosedAsync("t1", "u1", now);

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.Completed &&
            x.CompletedAt == now &&
            x.CompletedWithinResponseSla &&
            x.CompletedWithinResolutionSla));
    }

    [Fact]
    public async Task HandleTicketClosedAsync_AccountsForActivePause_WhenComputingCompletion()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();

        var now = DateTimeOffset.UtcNow;
        var pausedAt = now.AddMinutes(-45);
        var ticket = new Request { Id = "t2", State = TicketState.Resolved };
        var state = new TicketSlaState
        {
            TicketId = "t2",
            StartedAt = now.AddHours(-2),
            ResponseDueAt = now.AddMinutes(-10),
            ResolutionDueAt = now.AddMinutes(10),
            Status = SlaStatus.Paused,
            PausedAt = pausedAt,
            ResumeAt = now.AddMinutes(-1)
        };

        tickets.GetAsync("t2").Returns(ticket);
        states.GetByTicketIdForUpdateAsync("t2").Returns(state);

        var sut = new TicketSlaCompletionService(tickets, states, clock);

        await sut.HandleTicketClosedAsync("t2", "u1", now);

        await states.Received(1).UpdateAsync(Arg.Is<TicketSlaState>(x =>
            x.Status == SlaStatus.Completed &&
            x.CompletedWithinResolutionSla &&
            x.ResumeAt == null &&
            x.PausedAt == null &&
            x.AccumulatedPauseDuration >= TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public async Task HandleTicketClosedAsync_NoOp_WhenSlaStateMissing()
    {
        var tickets = Substitute.For<IRepository<Ticket>>();
        var states = Substitute.For<ITicketSlaRepository>();
        var clock = new SlaClockService();

        tickets.GetAsync("t3").Returns(new Change { Id = "t3", State = TicketState.Resolved });
        states.GetByTicketIdForUpdateAsync("t3").Returns((TicketSlaState?)null);

        var sut = new TicketSlaCompletionService(tickets, states, clock);

        await sut.HandleTicketClosedAsync("t3", "u1", DateTimeOffset.UtcNow);

        await states.DidNotReceive().UpdateAsync(Arg.Any<TicketSlaState>());
    }
}
