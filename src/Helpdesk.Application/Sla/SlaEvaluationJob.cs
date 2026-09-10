using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Application.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Application.Sla;

public class SlaEvaluationJob(
    ITicketSlaQueryRepository queryRepository,
    ITicketSlaRepository ticketSlaRepository,
    ISlaClockService slaClockService,
    ISlaEscalationEvaluator escalationEvaluator,
    IOptions<SlaEvaluationJobSettings> settings,
    ILogger<SlaEvaluationJob> logger,
    IWorkingCalendarResolver? calendarResolver = null,
    IBusinessTimeCalculator? businessTimeCalculator = null,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ISlaEvaluationJob
{
    private readonly ITicketSlaQueryRepository _queryRepository = queryRepository;
    private readonly ITicketSlaRepository _ticketSlaRepository = ticketSlaRepository;
    private readonly ISlaClockService _slaClockService = slaClockService;
    private readonly ISlaEscalationEvaluator _escalationEvaluator = escalationEvaluator;
    private readonly IWorkingCalendarResolver _calendarResolver = calendarResolver ?? new NullWorkingCalendarResolver();
    private readonly IBusinessTimeCalculator _businessTimeCalculator = businessTimeCalculator ?? new BusinessTimeCalculator();
    private readonly ILogger<SlaEvaluationJob> _logger = logger;
    private readonly int _batchSize = Math.Max(1, settings.Value.BatchSize);
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task RunAsync(CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var jobId = $"sla-eval-{startedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var correlationId = GetCorrelationId();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var processed = 0;
        var resumed = 0;
        var breached = 0;
        var failures = 0;
        var escalationsSent = 0;
        string? cursor = null;
        var calendarCache = new Dictionary<string, WorkingCalendar?>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("SLA evaluation job started. BatchSize={BatchSize}", _batchSize);
        await _domainEvents.PublishAsync(
            new SlaEvaluationJobStartedDomainEvent(
                jobId,
                startedAt,
                0,
                0,
                0,
                0,
                correlationId),
            ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var batch = await _queryRepository.GetBatchAsync(_batchSize, cursor, ct);
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var row in batch)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var result = await ProcessOneAsync(row, DateTimeOffset.UtcNow, calendarCache, ct);
                        processed++;
                        if (result.Resumed)
                        {
                            resumed++;
                        }

                        if (result.Breached)
                        {
                            breached++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        _logger.LogError(
                            ex,
                            "SLA evaluation failed for ticket {TicketId} tenant {TenantId}.",
                            row.TicketId,
                            row.TenantId);
                    }
                }

                cursor = batch[^1].Cursor;
                if (batch.Count < _batchSize)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _domainEvents.PublishAsync(
                new SlaEvaluationJobFailedDomainEvent(
                    jobId,
                    startedAt,
                    stopwatch.ElapsedMilliseconds,
                    processed,
                    escalationsSent,
                    failures + 1,
                    ex.Message,
                    correlationId),
                ct);
            throw;
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "SLA evaluation job finished. StartedAt={StartedAt} DurationMs={DurationMs} Processed={Processed} Resumed={Resumed} Breached={Breached} Failures={Failures}",
            startedAt,
            stopwatch.ElapsedMilliseconds,
            processed,
            resumed,
            breached,
            failures);
        await _domainEvents.PublishAsync(
            new SlaEvaluationJobCompletedDomainEvent(
                jobId,
                startedAt,
                stopwatch.ElapsedMilliseconds,
                processed,
                escalationsSent,
                failures,
                correlationId),
            ct);
    }

    private async Task<(bool Resumed, bool Breached)> ProcessOneAsync(
        TicketSlaBatchRow row,
        DateTimeOffset nowUtc,
        Dictionary<string, WorkingCalendar?> calendarCache,
        CancellationToken ct)
    {
        if (row.IsClosed || row.SlaState.Status == SlaStatus.Completed)
        {
            return default;
        }

        var state = await _ticketSlaRepository.GetByTicketIdForUpdateAsync(row.TicketId);
        if (state is null || state.Status == SlaStatus.Completed)
        {
            return default;
        }

        var resumed = false;
        var breached = false;
        var changed = false;
        var calendar = await ResolveCalendarAsync(row.TenantId, calendarCache);

        if (state.Status == SlaStatus.Paused &&
            state.ResumeAt.HasValue &&
            state.ResumeAt.Value <= nowUtc)
        {
            if (state.PausedAt.HasValue && nowUtc > state.PausedAt.Value)
            {
                state.AccumulatedPauseDuration += nowUtc - state.PausedAt.Value;
                if (state.IsBusinessHours && calendar is not null)
                {
                    state.AccumulatedPauseWorkingSeconds += _businessTimeCalculator.GetWorkingSecondsBetween(
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
            resumed = true;
            changed = true;
            await _domainEvents.PublishAsync(
                new TicketSlaAutoResumedDomainEvent(
                    row.TicketId,
                    row.TenantId,
                    null,
                    state.Status,
                    nowUtc,
                    SlaTriggerSources.Hangfire,
                    GetCorrelationId()),
                ct);
        }

        var snapshot = _slaClockService.Compute(state, nowUtc, calendar);
        if (snapshot.ResponseBreached && !state.ResponseBreached)
        {
            state.ResponseBreached = true;
            changed = true;
            breached = true;
            await _domainEvents.PublishAsync(
                new TicketSlaBreachedDomainEvent(
                    row.TicketId,
                    row.TenantId,
                    SlaMetricType.Response,
                    state.Status,
                    nowUtc,
                    SlaTriggerSources.Hangfire,
                    GetCorrelationId()),
                ct);
        }

        if (snapshot.ResolutionBreached && !state.ResolutionBreached)
        {
            state.ResolutionBreached = true;
            changed = true;
            breached = true;
            await _domainEvents.PublishAsync(
                new TicketSlaBreachedDomainEvent(
                    row.TicketId,
                    row.TenantId,
                    SlaMetricType.Resolution,
                    state.Status,
                    nowUtc,
                    SlaTriggerSources.Hangfire,
                    GetCorrelationId()),
                ct);
        }

        if (snapshot.ResolutionBreached && state.Status != SlaStatus.Breached && state.Status != SlaStatus.Completed)
        {
            state.Status = SlaStatus.Breached;
            changed = true;
            breached = true;
        }

        if (changed)
        {
            await _ticketSlaRepository.UpdateAsync(state);
        }

        var ticketProjection = CreateTicketProjection(row);
        await _escalationEvaluator.EvaluateAndNotifyAsync(ticketProjection, state, nowUtc, ct);
        return (resumed, breached);
    }

    private async Task<WorkingCalendar?> ResolveCalendarAsync(string? tenantId, Dictionary<string, WorkingCalendar?> calendarCache)
    {
        var key = string.IsNullOrWhiteSpace(tenantId) ? "(system)" : tenantId;
        if (calendarCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resolved = await _calendarResolver.ResolveAsync(tenantId);
        calendarCache[key] = resolved;
        return resolved;
    }

    private static Ticket CreateTicketProjection(TicketSlaBatchRow row)
    {
        Ticket ticket = row.TicketType switch
        {
            TicketType.Incident => new Incident(),
            TicketType.Request => new Request(),
            TicketType.Change => new Change(),
            _ => new Incident()
        };

        ticket.Id = row.TicketId;
        ticket.OrganizationId = row.TenantId;
        ticket.Title = row.Title;
        ticket.TrackingId = row.TicketNumber ?? row.TicketId;
        ticket.Priority = row.Priority;
        ticket.ServiceId = row.ServiceId;
        ticket.State = row.IsClosed ? TicketState.Resolved : TicketState.InProgress;
        return ticket;
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
