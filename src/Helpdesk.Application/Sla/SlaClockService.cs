using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public class SlaClockService : ISlaClockService
{
    private readonly IBusinessTimeCalculator _businessTimeCalculator;

    public SlaClockService(IBusinessTimeCalculator? businessTimeCalculator = null)
    {
        _businessTimeCalculator = businessTimeCalculator ?? new BusinessTimeCalculator();
    }

    public SlaClockSnapshot Compute(TicketSlaState state, DateTimeOffset nowUtc, WorkingCalendar? calendar = null)
    {
        if (state.IsBusinessHours && calendar is not null)
        {
            return ComputeBusinessHours(state, nowUtc, calendar);
        }

        return ComputeTwentyFourSeven(state, nowUtc);
    }

    private SlaClockSnapshot ComputeTwentyFourSeven(TicketSlaState state, DateTimeOffset nowUtc)
    {
        var accumulatedPause = state.AccumulatedPauseDuration;
        if (state.Status == SlaStatus.Paused && state.PausedAt.HasValue && nowUtc > state.PausedAt.Value)
        {
            accumulatedPause += nowUtc - state.PausedAt.Value;
        }

        var elapsed = nowUtc - state.StartedAt - accumulatedPause;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var responseTotal = state.ResponseDueAt - state.StartedAt;
        var resolutionTotal = state.ResolutionDueAt - state.StartedAt;

        var responseRemaining = ClampRemaining(responseTotal - elapsed);
        var resolutionRemaining = ClampRemaining(resolutionTotal - elapsed);

        var responseBreached = state.ResponseBreached || IsBreached(elapsed, responseTotal);
        var resolutionBreached = state.ResolutionBreached || IsBreached(elapsed, resolutionTotal);

        var effectiveStatus = resolutionBreached
            ? SlaStatus.Breached
            : state.Status;

        return new SlaClockSnapshot(
            effectiveStatus,
            state.StartedAt,
            state.ResponseDueAt,
            state.ResolutionDueAt,
            accumulatedPause,
            state.PausedAt,
            state.ResumeAt,
            state.PauseReason,
            responseRemaining,
            resolutionRemaining,
            CalculatePercentUsed(elapsed, responseTotal),
            CalculatePercentUsed(elapsed, resolutionTotal),
            responseBreached,
            resolutionBreached);
    }

    private SlaClockSnapshot ComputeBusinessHours(TicketSlaState state, DateTimeOffset nowUtc, WorkingCalendar calendar)
    {
        var accumulatedPauseWorkingSeconds = state.AccumulatedPauseWorkingSeconds;
        if (state.Status == SlaStatus.Paused && state.PausedAt.HasValue && nowUtc > state.PausedAt.Value)
        {
            accumulatedPauseWorkingSeconds += _businessTimeCalculator.GetWorkingSecondsBetween(
                state.PausedAt.Value,
                nowUtc,
                calendar);
        }

        var elapsedWorkingSeconds = _businessTimeCalculator.GetWorkingSecondsBetween(state.StartedAt, nowUtc, calendar) - accumulatedPauseWorkingSeconds;
        if (elapsedWorkingSeconds < 0)
        {
            elapsedWorkingSeconds = 0;
        }

        var responseTotalSeconds = Math.Max(0, _businessTimeCalculator.GetWorkingSecondsBetween(state.StartedAt, state.ResponseDueAt, calendar));
        var resolutionTotalSeconds = Math.Max(0, _businessTimeCalculator.GetWorkingSecondsBetween(state.StartedAt, state.ResolutionDueAt, calendar));

        var responseRemainingSeconds = Math.Max(0, responseTotalSeconds - elapsedWorkingSeconds);
        var resolutionRemainingSeconds = Math.Max(0, resolutionTotalSeconds - elapsedWorkingSeconds);
        var responseBreached = state.ResponseBreached || IsBreached(elapsedWorkingSeconds, responseTotalSeconds);
        var resolutionBreached = state.ResolutionBreached || IsBreached(elapsedWorkingSeconds, resolutionTotalSeconds);

        var effectiveStatus = resolutionBreached
            ? SlaStatus.Breached
            : state.Status;

        return new SlaClockSnapshot(
            effectiveStatus,
            state.StartedAt,
            state.ResponseDueAt,
            state.ResolutionDueAt,
            TimeSpan.FromSeconds(accumulatedPauseWorkingSeconds),
            state.PausedAt,
            state.ResumeAt,
            state.PauseReason,
            TimeSpan.FromSeconds(responseRemainingSeconds),
            TimeSpan.FromSeconds(resolutionRemainingSeconds),
            CalculatePercentUsed(elapsedWorkingSeconds, responseTotalSeconds),
            CalculatePercentUsed(elapsedWorkingSeconds, resolutionTotalSeconds),
            responseBreached,
            resolutionBreached);
    }

    private static bool IsBreached(TimeSpan elapsed, TimeSpan total)
    {
        return total > TimeSpan.Zero && elapsed > total;
    }

    private static bool IsBreached(long elapsedSeconds, long totalSeconds)
    {
        return totalSeconds > 0 && elapsedSeconds > totalSeconds;
    }

    private static TimeSpan ClampRemaining(TimeSpan value)
    {
        return value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private static int CalculatePercentUsed(TimeSpan elapsed, TimeSpan total)
    {
        if (total <= TimeSpan.Zero)
        {
            return elapsed > TimeSpan.Zero ? 100 : 0;
        }

        var percent = (elapsed.TotalSeconds / total.TotalSeconds) * 100d;
        if (percent < 0d) return 0;
        if (percent > 100d) return 100;
        return (int)Math.Round(percent, MidpointRounding.AwayFromZero);
    }

    private static int CalculatePercentUsed(long elapsedSeconds, long totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return elapsedSeconds > 0 ? 100 : 0;
        }

        var percent = (elapsedSeconds * 100d) / totalSeconds;
        if (percent < 0d) return 0;
        if (percent > 100d) return 100;
        return (int)Math.Round(percent, MidpointRounding.AwayFromZero);
    }
}
