using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Events;

namespace Helpdesk.Application.Sla;

public class TicketSlaCompletionService(
    IRepository<Ticket> ticketRepository,
    ITicketSlaRepository ticketSlaRepository,
    ISlaClockService slaClockService,
    IWorkingCalendarResolver? calendarResolver = null,
    IBusinessTimeCalculator? businessTimeCalculator = null,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ITicketSlaCompletionService
{
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task HandleTicketClosedAsync(string ticketId, string closedByUserId, DateTimeOffset nowUtc)
    {
        _ = closedByUserId;

        var ticket = await ticketRepository.GetAsync(ticketId);
        if (ticket is null || ticket.State != TicketState.Resolved)
        {
            return;
        }

        var state = await ticketSlaRepository.GetByTicketIdForUpdateAsync(ticketId);
        if (state is null || state.Status == SlaStatus.Completed)
        {
            return;
        }

        var resolver = calendarResolver ?? new NullWorkingCalendarResolver();
        var calculator = businessTimeCalculator ?? new BusinessTimeCalculator();
        var calendar = state.IsBusinessHours
            ? await resolver.ResolveAsync(ticket.OrganizationId)
            : null;
        var changed = ApplyAutoResumeIfDue(state, nowUtc, calendar, calculator);
        var snapshot = slaClockService.Compute(state, nowUtc, calendar);

        state.CompletedAt = nowUtc;
        state.Status = SlaStatus.Completed;
        state.CompletedWithinResponseSla = !snapshot.ResponseBreached;
        state.CompletedWithinResolutionSla = !snapshot.ResolutionBreached;

        await ticketSlaRepository.UpdateAsync(state);
        await _domainEvents.PublishAsync(
            new TicketSlaCompletedDomainEvent(
                ticketId,
                ticket.OrganizationId,
                null,
                state.Status,
                nowUtc,
                SlaTriggerSources.System,
                GetCorrelationId()),
            CancellationToken.None);

        if (changed)
        {
            await _domainEvents.PublishAsync(
                new TicketSlaAutoResumedDomainEvent(
                    ticketId,
                    ticket.OrganizationId,
                    null,
                    SlaStatus.InProgress,
                    nowUtc,
                    SlaTriggerSources.System,
                    GetCorrelationId()),
                CancellationToken.None);
        }

    }

    private static bool ApplyAutoResumeIfDue(
        TicketSlaState state,
        DateTimeOffset nowUtc,
        WorkingCalendar? calendar,
        IBusinessTimeCalculator businessTimeCalculator)
    {
        if (state.Status != SlaStatus.Paused ||
            !state.ResumeAt.HasValue ||
            state.ResumeAt.Value > nowUtc)
        {
            return false;
        }

        if (state.PausedAt.HasValue && nowUtc > state.PausedAt.Value)
        {
            state.AccumulatedPauseDuration += nowUtc - state.PausedAt.Value;
            if (state.IsBusinessHours && calendar is not null)
            {
                state.AccumulatedPauseWorkingSeconds += businessTimeCalculator.GetWorkingSecondsBetween(
                    state.PausedAt.Value,
                    nowUtc,
                    calendar);
            }
        }

        state.PausedAt = null;
        state.PauseReason = null;
        state.PausedByUserId = null;
        state.ResumeAt = null;
        state.LastResumedAt = nowUtc;
        state.Status = SlaStatus.InProgress;
        return true;
    }

    private sealed class NullWorkingCalendarResolver : IWorkingCalendarResolver
    {
        public Task<WorkingCalendar?> ResolveAsync(string? tenantId)
        {
            _ = tenantId;
            return Task.FromResult<WorkingCalendar?>(null);
        }
    }

    private string GetCorrelationId()
    {
        return _correlationContext?.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }
}
