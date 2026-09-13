using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Events;

namespace Helpdesk.Application.Sla;

public class TicketSlaService(
    IRepository<Ticket> tickets,
    ITicketSlaRepository ticketSlaRepository,
    ISlaPolicyResolver resolver,
    IWorkingCalendarResolver? calendarResolver = null,
    IBusinessTimeCalculator? businessTimeCalculator = null,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ITicketSlaService
{
    private readonly IRepository<Ticket> _tickets = tickets;
    private readonly ITicketSlaRepository _ticketSlaRepository = ticketSlaRepository;
    private readonly ISlaPolicyResolver _resolver = resolver;
    private readonly IWorkingCalendarResolver _calendarResolver = calendarResolver ?? new NullWorkingCalendarResolver();
    private readonly IBusinessTimeCalculator _businessTimeCalculator = businessTimeCalculator ?? new BusinessTimeCalculator();
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task PauseAsync(string ticketId, string userId, string reason)
    {
        var ticket = await _tickets.GetAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");
        await PauseAsync(ticket, userId, reason);
    }

    public async Task PauseAsync(Ticket ticket, string userId, string reason)
    {
        var state = await _ticketSlaRepository.GetByTicketIdForUpdateAsync(ticket.Id);
        if (state is null)
        {
            return;
        }

        if (state.Status is SlaStatus.Breached or SlaStatus.Completed)
        {
            throw new InvalidOperationException("SLA cannot be paused in the current status.");
        }

        var now = DateTimeOffset.UtcNow;
        if (state.Status == SlaStatus.Paused)
        {
            // Keep existing pause window unchanged when already paused.
            return;
        }

        state.PausedAt = now;
        state.PauseReason = reason;
        state.PausedByUserId = userId;
        state.Status = SlaStatus.Paused;
        state.ResumeAt = await ResolveAutoResumeAtAsync(ticket, now);
        await _ticketSlaRepository.UpdateAsync(state);
        await _domainEvents.PublishAsync(
            new TicketSlaPausedDomainEvent(
                ticket.Id,
                ticket.OrganizationId,
                null,
                state.Status,
                now,
                SlaTriggerSources.Manual,
                GetCorrelationId()),
            CancellationToken.None);
    }

    public async Task ResumeAsync(string ticketId, string userId)
    {
        var ticket = await _tickets.GetAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");
        await ResumeAsync(ticket, userId);
    }

    public async Task ResumeAsync(Ticket ticket, string userId)
    {
        var state = await _ticketSlaRepository.GetByTicketIdForUpdateAsync(ticket.Id);
        if (state is null || state.Status != SlaStatus.Paused)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var calendar = state.IsBusinessHours
            ? await _calendarResolver.ResolveAsync(ticket.OrganizationId)
            : null;
        ResumeState(state, nowUtc, calendar, _businessTimeCalculator);
        await _ticketSlaRepository.UpdateAsync(state);
        await _domainEvents.PublishAsync(
            new TicketSlaResumedDomainEvent(
                ticket.Id,
                ticket.OrganizationId,
                null,
                state.Status,
                nowUtc,
                SlaTriggerSources.Manual,
                GetCorrelationId()),
            CancellationToken.None);
    }

    public async Task AutoResumeIfDueAsync(string ticketId)
    {
        var state = await _ticketSlaRepository.GetByTicketIdForUpdateAsync(ticketId);
        if (state is null || state.Status != SlaStatus.Paused)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (state.ResumeAt.HasValue && state.ResumeAt.Value <= now)
        {
            var ticket = await _tickets.GetAsync(ticketId);
            var calendar = state.IsBusinessHours
                ? await _calendarResolver.ResolveAsync(ticket?.OrganizationId)
                : null;
            ResumeState(state, now, calendar, _businessTimeCalculator);
            await _ticketSlaRepository.UpdateAsync(state);
            await _domainEvents.PublishAsync(
                new TicketSlaAutoResumedDomainEvent(
                    ticketId,
                    ticket?.OrganizationId,
                    null,
                    state.Status,
                    now,
                    SlaTriggerSources.System,
                    GetCorrelationId()),
                CancellationToken.None);
        }
    }

    private async Task<DateTimeOffset?> ResolveAutoResumeAtAsync(Ticket ticket, DateTimeOffset now)
    {
        var ticketType = ResolveTicketType(ticket);
        if (ticketType is null)
        {
            return null;
        }

        var policy = await _resolver.ResolveAsync(ticket.OrganizationId, ticketType.Value, (int)ticket.Priority, ticket.ServiceId);
        policy ??= await _resolver.ResolveAsync(ticket.OrganizationId, ticketType.Value);
        if (policy is null)
        {
            return null;
        }

        if (policy.ScopeType != SlaScopeType.Tenant || !policy.AutoResumeAfterHours.HasValue)
        {
            return null;
        }

        return now.AddHours(policy.AutoResumeAfterHours.Value);
    }

    private static TicketType? ResolveTicketType(Ticket ticket)
    {
        return ticket switch
        {
            Incident => TicketType.Incident,
            Request => TicketType.Request,
            Change => TicketType.Change,
            _ => null
        };
    }

    private sealed class NullWorkingCalendarResolver : IWorkingCalendarResolver
    {
        public Task<WorkingCalendar?> ResolveAsync(string? tenantId)
        {
            _ = tenantId;
            return Task.FromResult<WorkingCalendar?>(null);
        }
    }

    private static void ResumeState(
        TicketSlaState state,
        DateTimeOffset now,
        WorkingCalendar? calendar,
        IBusinessTimeCalculator businessTimeCalculator)
    {
        if (state.PausedAt.HasValue && now > state.PausedAt.Value)
        {
            state.AccumulatedPauseDuration += now - state.PausedAt.Value;
            if (state.IsBusinessHours && calendar is not null)
            {
                state.AccumulatedPauseWorkingSeconds += businessTimeCalculator.GetWorkingSecondsBetween(
                    state.PausedAt.Value,
                    now,
                    calendar);
            }
        }

        state.PausedAt = null;
        state.PauseReason = null;
        state.PausedByUserId = null;
        state.ResumeAt = null;
        state.LastResumedAt = now;
        state.Status = SlaStatus.InProgress;
    }

    private string GetCorrelationId()
    {
        return _correlationContext?.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }
}
