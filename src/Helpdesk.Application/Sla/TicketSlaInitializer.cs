using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Application.Events;

namespace Helpdesk.Application.Sla;

public class TicketSlaInitializer(
    ISlaPolicyResolver resolver,
    ITicketSlaRepository slaRepository,
    ITenantSlaSettingsRepository? tenantSettings = null,
    IWorkingCalendarResolver? calendarResolver = null,
    IBusinessTimeCalculator? businessTimeCalculator = null,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ITicketSlaInitializer
{
    private readonly ISlaPolicyResolver _resolver = resolver;
    private readonly ITicketSlaRepository _slaRepository = slaRepository;
    private readonly ITenantSlaSettingsRepository _tenantSettings = tenantSettings ?? new NullTenantSlaSettingsRepository();
    private readonly IWorkingCalendarResolver _calendarResolver = calendarResolver ?? new NullWorkingCalendarResolver();
    private readonly IBusinessTimeCalculator _businessTimeCalculator = businessTimeCalculator ?? new BusinessTimeCalculator();
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task InitializeAsync(Ticket ticket)
    {
        var ticketType = ResolveTicketType(ticket);
        if (ticketType is null)
        {
            return;
        }

        var policy = await _resolver.ResolveAsync(
            ticket.OrganizationId,
            ticketType.Value,
            (int)ticket.Priority,
            ticket.ServiceId);
        policy ??= await _resolver.ResolveAsync(ticket.OrganizationId, ticketType.Value);
        if (policy is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var businessHoursEnabled = false;
        WorkingCalendar? calendar = null;
        if (!string.IsNullOrWhiteSpace(ticket.OrganizationId))
        {
            var settings = await _tenantSettings.GetByTenantIdAsync(ticket.OrganizationId);
            businessHoursEnabled = settings?.UseBusinessHours == true;
            if (businessHoursEnabled)
            {
                calendar = await _calendarResolver.ResolveAsync(ticket.OrganizationId);
            }
        }

        var responseDueAt = businessHoursEnabled && calendar is not null
            ? _businessTimeCalculator.AddWorkingSeconds(now, policy.ResponseTimeHours * 3600L, calendar)
            : now.AddHours(policy.ResponseTimeHours);
        var resolutionDueAt = businessHoursEnabled && calendar is not null
            ? _businessTimeCalculator.AddWorkingSeconds(now, policy.ResolutionTimeHours * 3600L, calendar)
            : now.AddHours(policy.ResolutionTimeHours);

        var state = new TicketSlaState
        {
            TicketId = ticket.Id,
            StartedAt = now,
            ResponseDueAt = responseDueAt,
            ResolutionDueAt = resolutionDueAt,
            IsBusinessHours = businessHoursEnabled && calendar is not null,
            CalendarId = calendar?.Id,
            Status = SlaStatus.InProgress,
            ResponseBreached = false,
            ResolutionBreached = false
        };

        await _slaRepository.AddAsync(state);
        await _domainEvents.PublishAsync(
            new TicketSlaInitializedDomainEvent(
                ticket.Id,
                ticket.OrganizationId,
                null,
                state.Status,
                now,
                SlaTriggerSources.System,
                GetCorrelationId()),
            CancellationToken.None);
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

    private sealed class NullTenantSlaSettingsRepository : ITenantSlaSettingsRepository
    {
        public Task<TenantSlaSettings?> GetByTenantIdAsync(string tenantId, CancellationToken ct = default)
        {
            _ = tenantId;
            _ = ct;
            return Task.FromResult<TenantSlaSettings?>(null);
        }
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
