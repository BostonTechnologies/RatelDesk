using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Application.Sla;

public class SlaClockServiceTests
{
    [Fact]
    public void Compute_WhenInProgress_CalculatesRemainingAndPercent()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var state = new TicketSlaState
        {
            TicketId = "t1",
            StartedAt = startedAt,
            ResponseDueAt = startedAt.AddHours(4),
            ResolutionDueAt = startedAt.AddHours(8),
            Status = SlaStatus.InProgress,
            AccumulatedPauseDuration = TimeSpan.Zero
        };
        var sut = new SlaClockService();

        var snapshot = sut.Compute(state, startedAt.AddHours(2));

        Assert.InRange(
            snapshot.ResponseRemaining,
            TimeSpan.FromHours(2) - TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(2) + TimeSpan.FromSeconds(1));
        Assert.InRange(
            snapshot.ResolutionRemaining,
            TimeSpan.FromHours(6) - TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(6) + TimeSpan.FromSeconds(1));
        Assert.Equal(50, snapshot.ResponsePercentUsed);
        Assert.Equal(25, snapshot.ResolutionPercentUsed);
        Assert.False(snapshot.ResponseBreached);
        Assert.False(snapshot.ResolutionBreached);
    }

    [Fact]
    public void Compute_WhenPaused_IncludesActivePauseInClock()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-6);
        var pausedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var state = new TicketSlaState
        {
            TicketId = "t2",
            StartedAt = startedAt,
            ResponseDueAt = startedAt.AddHours(8),
            ResolutionDueAt = startedAt.AddHours(12),
            Status = SlaStatus.Paused,
            PausedAt = pausedAt,
            AccumulatedPauseDuration = TimeSpan.FromHours(1),
            PauseReason = "AgentResponded"
        };
        var sut = new SlaClockService();

        var snapshot = sut.Compute(state, DateTimeOffset.UtcNow);

        Assert.Equal("AgentResponded", snapshot.PauseReason);
        Assert.False(snapshot.ResponseBreached);
        Assert.False(snapshot.ResolutionBreached);
        Assert.True(snapshot.ResponseRemaining > TimeSpan.Zero);
    }

    [Fact]
    public void Compute_WhenElapsedExceedsTotals_FlagsBreaches()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-10);
        var state = new TicketSlaState
        {
            TicketId = "t3",
            StartedAt = startedAt,
            ResponseDueAt = startedAt.AddHours(1),
            ResolutionDueAt = startedAt.AddHours(2),
            Status = SlaStatus.InProgress
        };
        var sut = new SlaClockService();

        var snapshot = sut.Compute(state, DateTimeOffset.UtcNow);

        Assert.True(snapshot.ResponseBreached);
        Assert.True(snapshot.ResolutionBreached);
        Assert.Equal(SlaStatus.Breached, snapshot.Status);
        Assert.Equal(TimeSpan.Zero, snapshot.ResponseRemaining);
        Assert.Equal(TimeSpan.Zero, snapshot.ResolutionRemaining);
    }
}
