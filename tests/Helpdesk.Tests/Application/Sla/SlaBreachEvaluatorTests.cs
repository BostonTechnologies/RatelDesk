using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Application.Sla;

public class SlaBreachEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenDueDatesPassed_FlagsBreachesAndStatus()
    {
        var state = new TicketSlaState
        {
            TicketId = "inc-1",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-4),
            ResponseDueAt = DateTimeOffset.UtcNow.AddHours(-2),
            ResolutionDueAt = DateTimeOffset.UtcNow.AddHours(-1),
            Status = SlaStatus.InProgress,
            ResponseBreached = false,
            ResolutionBreached = false
        };
        var sut = new SlaBreachEvaluator();

        sut.Evaluate(state);

        Assert.True(state.ResponseBreached);
        Assert.True(state.ResolutionBreached);
        Assert.Equal(SlaStatus.Breached, state.Status);
    }

    [Fact]
    public void Evaluate_WhenDueDatesNotPassed_DoesNotFlagBreaches()
    {
        var state = new TicketSlaState
        {
            TicketId = "inc-2",
            StartedAt = DateTimeOffset.UtcNow,
            ResponseDueAt = DateTimeOffset.UtcNow.AddHours(2),
            ResolutionDueAt = DateTimeOffset.UtcNow.AddHours(8),
            Status = SlaStatus.InProgress,
            ResponseBreached = false,
            ResolutionBreached = false
        };
        var sut = new SlaBreachEvaluator();

        sut.Evaluate(state);

        Assert.False(state.ResponseBreached);
        Assert.False(state.ResolutionBreached);
        Assert.Equal(SlaStatus.InProgress, state.Status);
    }
}
