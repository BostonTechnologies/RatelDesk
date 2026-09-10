using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Reports;

public static class SlaReportEndpoints
{
    public static void MapSlaReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports/sla")
            .WithTags("SLA Reports")
            .RequireAuthorization();

        group.MapGet("/compliance", async (
            [FromQuery] string? tenantId,
            [FromQuery] TicketType? ticketType,
            [FromQuery] DateTimeOffset fromUtc,
            [FromQuery] DateTimeOffset toUtc,
            [FromServices] ITenantContext tenantContext,
            [FromServices] ISlaReportingQueryService reporting,
            CancellationToken ct) =>
        {
            var resolvedTenant = ResolveTenantScope(tenantContext, tenantId);
            if (!resolvedTenant.Allowed)
            {
                return Results.Forbid();
            }

            var result = await reporting.GetComplianceSummaryAsync(new SlaComplianceQuery
            {
                TenantId = resolvedTenant.TenantId,
                TicketType = ticketType,
                FromUtc = fromUtc,
                ToUtc = toUtc
            }, ct);

            return Results.Ok(result);
        })
        .WithName("GetSlaComplianceSummary")
        .WithSummary("Gets SLA compliance summary for a period.");

        group.MapGet("/breached", async (
            [FromQuery] string? tenantId,
            [FromQuery] TicketType? ticketType,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] ITenantContext tenantContext,
            [FromServices] ISlaReportingQueryService reporting,
            CancellationToken ct) =>
        {
            var resolvedTenant = ResolveTenantScope(tenantContext, tenantId);
            if (!resolvedTenant.Allowed)
            {
                return Results.Forbid();
            }

            var result = await reporting.GetBreachedTicketsAsync(new SlaTicketListQuery
            {
                TenantId = resolvedTenant.TenantId,
                TicketType = ticketType,
                Page = page,
                PageSize = pageSize
            }, ct);

            return Results.Ok(result);
        })
        .WithName("GetSlaBreachedTickets")
        .WithSummary("Gets open breached SLA tickets.");

        group.MapGet("/near-breach", async (
            [FromQuery] string? tenantId,
            [FromQuery] TicketType? ticketType,
            [FromQuery] SlaMetricType metric,
            [FromQuery] int thresholdPercent,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] ITenantContext tenantContext,
            [FromServices] ISlaReportingQueryService reporting,
            CancellationToken ct) =>
        {
            var resolvedTenant = ResolveTenantScope(tenantContext, tenantId);
            if (!resolvedTenant.Allowed)
            {
                return Results.Forbid();
            }

            var result = await reporting.GetNearBreachTicketsAsync(new SlaNearBreachQuery
            {
                TenantId = resolvedTenant.TenantId,
                TicketType = ticketType,
                Metric = metric,
                ThresholdPercent = thresholdPercent <= 0 ? 80 : thresholdPercent,
                Page = page,
                PageSize = pageSize
            }, ct);

            return Results.Ok(result);
        })
        .WithName("GetSlaNearBreachTickets")
        .WithSummary("Gets open near-breach SLA tickets.");

        group.MapGet("/completed", async (
            [FromQuery] string? tenantId,
            [FromQuery] TicketType? ticketType,
            [FromQuery] DateTimeOffset fromUtc,
            [FromQuery] DateTimeOffset toUtc,
            [FromQuery] bool? withinResolutionSla,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] ITenantContext tenantContext,
            [FromServices] ISlaReportingQueryService reporting,
            CancellationToken ct) =>
        {
            var resolvedTenant = ResolveTenantScope(tenantContext, tenantId);
            if (!resolvedTenant.Allowed)
            {
                return Results.Forbid();
            }

            var result = await reporting.GetCompletedTicketsAsync(new SlaCompletedQuery
            {
                TenantId = resolvedTenant.TenantId,
                TicketType = ticketType,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WithinResolutionSla = withinResolutionSla,
                Page = page,
                PageSize = pageSize
            }, ct);

            return Results.Ok(result);
        })
        .WithName("GetSlaCompletedTickets")
        .WithSummary("Gets completed tickets with SLA outcomes.");
    }

    private static TenantScopeResolution ResolveTenantScope(ITenantContext tenantContext, string? requestedTenantId)
    {
        if (tenantContext.IsHelpdeskAdmin)
        {
            return new TenantScopeResolution(true, requestedTenantId);
        }

        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return new TenantScopeResolution(false, null);
        }

        return new TenantScopeResolution(true, tenantContext.TenantId);
    }

    private readonly record struct TenantScopeResolution(bool Allowed, string? TenantId);
}
