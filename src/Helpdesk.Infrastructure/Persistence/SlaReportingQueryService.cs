using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Persistence;

public class SlaReportingQueryService(
    HelpdeskDbContext context,
    ISlaClockService slaClockService,
    IOptions<SlaReportingOptions> options,
    IWorkingCalendarResolver? calendarResolver = null) : ISlaReportingQueryService
{
    private readonly HelpdeskDbContext _context = context;
    private readonly ISlaClockService _slaClockService = slaClockService;
    private readonly IWorkingCalendarResolver _calendarResolver = calendarResolver ?? new NullWorkingCalendarResolver();
    private readonly int _nearBreachCandidateLimit = Math.Max(100, options.Value.NearBreachCandidateLimit);

    public async Task<SlaComplianceSummaryDto> GetComplianceSummaryAsync(SlaComplianceQuery query, CancellationToken ct)
    {
        var completedQuery = BuildCompletedQuery(query.TenantId, query.TicketType)
            .Where(x => x.Sla.CompletedAt.HasValue)
            .Where(x => x.Sla.CompletedAt!.Value >= query.FromUtc && x.Sla.CompletedAt!.Value <= query.ToUtc);

        var rows = await completedQuery
            .Select(x => new
            {
                CompletedAt = x.Sla.CompletedAt!.Value,
                x.Sla.CompletedWithinResolutionSla,
                x.Sla.CompletedWithinResponseSla
            })
            .ToListAsync(ct);

        var completedTotal = rows.Count;
        var withinResolution = rows.Count(x => x.CompletedWithinResolutionSla);
        var withinResponse = rows.Count(x => x.CompletedWithinResponseSla);

        var dto = new SlaComplianceSummaryDto
        {
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            CompletedTotal = completedTotal,
            CompletedWithinResolutionSla = withinResolution,
            CompletedBreachedResolutionSla = completedTotal - withinResolution,
            CompletedWithinResponseSla = withinResponse,
            CompletedBreachedResponseSla = completedTotal - withinResponse,
            ResolutionCompliancePercent = completedTotal == 0 ? 0 : Math.Round(withinResolution * 100d / completedTotal, 2),
            ResponseCompliancePercent = completedTotal == 0 ? 0 : Math.Round(withinResponse * 100d / completedTotal, 2),
            Buckets = rows
                .GroupBy(x => x.CompletedAt.UtcDateTime.Date)
                .OrderBy(x => x.Key)
                .Select(group => new SlaComplianceBucketDto
                {
                    Label = group.Key.ToString("yyyy-MM-dd"),
                    CompletedTotal = group.Count(),
                    CompletedWithinResolutionSla = group.Count(x => x.CompletedWithinResolutionSla)
                })
                .ToList()
        };

        return dto;
    }

    public async Task<PagedResult<SlaTicketRowDto>> GetBreachedTicketsAsync(SlaTicketListQuery query, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var baseQuery = BuildActiveQuery(query.TenantId, query.TicketType)
            .Where(x => x.Sla.Status == SlaStatus.Breached || x.Sla.ResponseBreached || x.Sla.ResolutionBreached);

        var total = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderBy(x => x.Sla.ResolutionDueAt)
            .ThenBy(x => x.Sla.ResponseDueAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var calendarCache = await BuildCalendarCacheAsync(rows.Select(x => x.Ticket.OrganizationId), ct);

        return new PagedResult<SlaTicketRowDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = rows.Select(x => ToSlaTicketRow(x, nowUtc, calendarCache)).ToList()
        };
    }

    public async Task<PagedResult<SlaTicketRowDto>> GetNearBreachTicketsAsync(SlaNearBreachQuery query, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);
        var threshold = Math.Clamp(query.ThresholdPercent, 1, 100);

        var candidatesQuery = BuildActiveQuery(query.TenantId, query.TicketType)
            .Where(x => x.Sla.Status != SlaStatus.Completed);
        candidatesQuery = query.Metric == SlaMetricType.Response
            ? candidatesQuery.OrderBy(x => x.Sla.ResponseDueAt)
            : candidatesQuery.OrderBy(x => x.Sla.ResolutionDueAt);

        candidatesQuery = candidatesQuery.Take(_nearBreachCandidateLimit);

        var candidates = await candidatesQuery.ToListAsync(ct);

        var calendarCache = await BuildCalendarCacheAsync(candidates.Select(x => x.Ticket.OrganizationId), ct);
        var filtered = candidates
            .Select(x => new
            {
                Row = x,
                Snapshot = _slaClockService.Compute(
                    x.Sla,
                    nowUtc,
                    ResolveCalendar(x.Ticket.OrganizationId, calendarCache))
            })
            .Where(x => query.Metric == SlaMetricType.Response
                ? !x.Snapshot.ResponseBreached && x.Snapshot.ResponsePercentUsed >= threshold
                : !x.Snapshot.ResolutionBreached && x.Snapshot.ResolutionPercentUsed >= threshold)
            .OrderBy(x => query.Metric == SlaMetricType.Response
                ? x.Row.Sla.ResponseDueAt
                : x.Row.Sla.ResolutionDueAt)
            .ToList();

        var total = filtered.Count;
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToSlaTicketRow(x.Row, nowUtc, calendarCache, x.Snapshot))
            .ToList();

        return new PagedResult<SlaTicketRowDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    public async Task<PagedResult<SlaCompletedRowDto>> GetCompletedTicketsAsync(SlaCompletedQuery query, CancellationToken ct)
    {
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var completedQuery = BuildCompletedQuery(query.TenantId, query.TicketType)
            .Where(x => x.Sla.CompletedAt.HasValue)
            .Where(x => x.Sla.CompletedAt!.Value >= query.FromUtc && x.Sla.CompletedAt!.Value <= query.ToUtc);

        if (query.WithinResolutionSla.HasValue)
        {
            completedQuery = completedQuery.Where(x => x.Sla.CompletedWithinResolutionSla == query.WithinResolutionSla.Value);
        }

        var total = await completedQuery.CountAsync(ct);

        var rows = await completedQuery
            .OrderByDescending(x => x.Sla.CompletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SlaCompletedRowDto
            {
                TicketId = x.Ticket.Id,
                TicketNumber = x.Ticket.TrackingId,
                Title = x.Ticket.Title,
                TenantId = x.Ticket.OrganizationId ?? string.Empty,
                TicketType = ResolveTicketType(x.Discriminator),
                ServiceId = x.Ticket.ServiceId,
                Priority = (int)x.Ticket.Priority,
                CompletedAt = x.Sla.CompletedAt!.Value,
                WithinResponseSla = x.Sla.CompletedWithinResponseSla,
                WithinResolutionSla = x.Sla.CompletedWithinResolutionSla
            })
            .ToListAsync(ct);

        return new PagedResult<SlaCompletedRowDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = rows
        };
    }

    private IQueryable<ActiveSlaQueryRow> BuildActiveQuery(string? tenantId, TicketType? ticketType)
    {
        var query =
            from state in _context.TicketSlaStates.AsNoTracking()
            join ticket in _context.Tickets.AsNoTracking() on state.TicketId equals ticket.Id
            where ticket.State != TicketState.Resolved
            where state.Status != SlaStatus.Completed
            select new ActiveSlaQueryRow
            {
                Ticket = ticket,
                Sla = state,
                Discriminator = EF.Property<string>(ticket, "Discriminator")
            };

        query = ApplyTenantFilter(query, tenantId);
        query = ApplyTicketTypeFilter(query, ticketType);
        return query;
    }

    private IQueryable<ActiveSlaQueryRow> BuildCompletedQuery(string? tenantId, TicketType? ticketType)
    {
        var query =
            from state in _context.TicketSlaStates.AsNoTracking()
            join ticket in _context.Tickets.AsNoTracking() on state.TicketId equals ticket.Id
            where state.Status == SlaStatus.Completed
            select new ActiveSlaQueryRow
            {
                Ticket = ticket,
                Sla = state,
                Discriminator = EF.Property<string>(ticket, "Discriminator")
            };

        query = ApplyTenantFilter(query, tenantId);
        query = ApplyTicketTypeFilter(query, ticketType);
        return query;
    }

    private static IQueryable<ActiveSlaQueryRow> ApplyTenantFilter(IQueryable<ActiveSlaQueryRow> query, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return query;
        }

        return query.Where(x => x.Ticket.OrganizationId == tenantId);
    }

    private static IQueryable<ActiveSlaQueryRow> ApplyTicketTypeFilter(IQueryable<ActiveSlaQueryRow> query, TicketType? ticketType)
    {
        if (!ticketType.HasValue)
        {
            return query;
        }

        return ticketType.Value switch
        {
            TicketType.Request => query.Where(x => x.Discriminator == nameof(Request)),
            TicketType.Change => query.Where(x => x.Discriminator == nameof(Change)),
            _ => query.Where(x => x.Discriminator == nameof(Incident))
        };
    }

    private SlaTicketRowDto ToSlaTicketRow(
        ActiveSlaQueryRow row,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, WorkingCalendar?> calendarCache,
        SlaClockSnapshot? snapshot = null)
    {
        snapshot ??= _slaClockService.Compute(
            row.Sla,
            nowUtc,
            ResolveCalendar(row.Ticket.OrganizationId, calendarCache));

        return new SlaTicketRowDto
        {
            TicketId = row.Ticket.Id,
            TicketNumber = row.Ticket.TrackingId,
            Title = row.Ticket.Title,
            TenantId = row.Ticket.OrganizationId ?? string.Empty,
            TicketType = ResolveTicketType(row.Discriminator),
            SlaStatus = snapshot.Status,
            ResponseBreached = snapshot.ResponseBreached,
            ResolutionBreached = snapshot.ResolutionBreached,
            StartedAt = snapshot.StartedAt,
            ResponseDueAt = snapshot.ResponseDueAt,
            ResolutionDueAt = snapshot.ResolutionDueAt,
            ResponseRemainingSeconds = (long)snapshot.ResponseRemaining.TotalSeconds,
            ResolutionRemainingSeconds = (long)snapshot.ResolutionRemaining.TotalSeconds,
            ResponsePercentUsed = snapshot.ResponsePercentUsed,
            ResolutionPercentUsed = snapshot.ResolutionPercentUsed
        };
    }

    private static TicketType ResolveTicketType(string? discriminator)
    {
        return discriminator switch
        {
            nameof(Request) => TicketType.Request,
            nameof(Change) => TicketType.Change,
            _ => TicketType.Incident
        };
    }

    private static int NormalizePage(int page)
    {
        return page < 1 ? 1 : page;
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            return 50;
        }

        return pageSize > 200 ? 200 : pageSize;
    }

    private sealed class ActiveSlaQueryRow
    {
        public required Ticket Ticket { get; init; }
        public required TicketSlaState Sla { get; init; }
        public string? Discriminator { get; init; }
    }

    private async Task<Dictionary<string, WorkingCalendar?>> BuildCalendarCacheAsync(IEnumerable<string?> tenantIds, CancellationToken ct)
    {
        var cache = new Dictionary<string, WorkingCalendar?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tenantId in tenantIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            cache[tenantId!] = await _calendarResolver.ResolveAsync(tenantId);
        }

        return cache;
    }

    private static WorkingCalendar? ResolveCalendar(string? tenantId, IReadOnlyDictionary<string, WorkingCalendar?> calendarCache)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return calendarCache.TryGetValue(tenantId, out var value) ? value : null;
    }

    private sealed class NullWorkingCalendarResolver : IWorkingCalendarResolver
    {
        public Task<WorkingCalendar?> ResolveAsync(string? tenantId)
        {
            _ = tenantId;
            return Task.FromResult<WorkingCalendar?>(null);
        }
    }
}
