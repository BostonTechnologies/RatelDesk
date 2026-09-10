using Helpdesk.Application.Events;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Models;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Sla;

public static class TenantSlaSettingsEndpoints
{
    public static void MapTenantSlaSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenant-sla-settings")
            .WithTags("Tenant SLA Settings")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/{tenantId}", async (string tenantId, [FromServices] HelpdeskDbContext db) =>
        {
            var row = await db.TenantSlaSettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId);
            if (row is null)
            {
                return Results.Ok(new TenantSlaSettingsDto
                {
                    TenantId = tenantId,
                    UseBusinessHours = false,
                    CalendarId = null
                });
            }

            return Results.Ok(new TenantSlaSettingsDto
            {
                TenantId = row.TenantId,
                UseBusinessHours = row.UseBusinessHours,
                CalendarId = row.CalendarId
            });
        });

        group.MapPut("/{tenantId}", async (
            string tenantId,
            [FromBody] TenantSlaSettingsDto dto,
            [FromServices] HelpdeskDbContext db,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await db.TenantSlaSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId);
            var requestedCalendarId = string.IsNullOrWhiteSpace(dto.CalendarId) ? null : dto.CalendarId.Trim();
            var actor = GetActorId(httpContext);
            var now = DateTimeOffset.UtcNow;
            var correlationId = GetCorrelationId(correlationContext);

            if (existing is null)
            {
                var created = new TenantSlaSettings
                {
                    TenantId = tenantId,
                    UseBusinessHours = dto.UseBusinessHours,
                    CalendarId = requestedCalendarId
                };
                db.TenantSlaSettings.Add(created);
                await db.SaveChangesAsync();
                await domainEvents.PublishAsync(
                    new TenantSlaSettingsUpdatedDomainEvent(
                        created.TenantId,
                        created.UseBusinessHours,
                        created.CalendarId,
                        actor,
                        now,
                        correlationId),
                    ct);
                if (created.UseBusinessHours)
                {
                    await domainEvents.PublishAsync(
                        new BusinessHoursEnabledDomainEvent(
                            created.TenantId,
                            created.UseBusinessHours,
                            created.CalendarId,
                            actor,
                            now,
                            correlationId),
                        ct);
                }
                else
                {
                    await domainEvents.PublishAsync(
                        new BusinessHoursDisabledDomainEvent(
                            created.TenantId,
                            created.UseBusinessHours,
                            created.CalendarId,
                            actor,
                            now,
                            correlationId),
                        ct);
                }

                return Results.Ok(new TenantSlaSettingsDto
                {
                    TenantId = created.TenantId,
                    UseBusinessHours = created.UseBusinessHours,
                    CalendarId = created.CalendarId
                });
            }

            var wasBusinessHoursEnabled = existing.UseBusinessHours;
            existing.UseBusinessHours = dto.UseBusinessHours;
            existing.CalendarId = requestedCalendarId;
            await db.SaveChangesAsync();
            await domainEvents.PublishAsync(
                new TenantSlaSettingsUpdatedDomainEvent(
                    existing.TenantId,
                    existing.UseBusinessHours,
                    existing.CalendarId,
                    actor,
                    now,
                    correlationId),
                ct);
            if (!wasBusinessHoursEnabled && existing.UseBusinessHours)
            {
                await domainEvents.PublishAsync(
                    new BusinessHoursEnabledDomainEvent(
                        existing.TenantId,
                        existing.UseBusinessHours,
                        existing.CalendarId,
                        actor,
                        now,
                        correlationId),
                    ct);
            }

            if (wasBusinessHoursEnabled && !existing.UseBusinessHours)
            {
                await domainEvents.PublishAsync(
                    new BusinessHoursDisabledDomainEvent(
                        existing.TenantId,
                        existing.UseBusinessHours,
                        existing.CalendarId,
                        actor,
                        now,
                        correlationId),
                    ct);
            }

            return Results.Ok(new TenantSlaSettingsDto
            {
                TenantId = existing.TenantId,
                UseBusinessHours = existing.UseBusinessHours,
                CalendarId = existing.CalendarId
            });
        });
    }

    private static string? GetActorId(HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");
    }

    private static string GetCorrelationId(ICorrelationContext correlationContext)
    {
        return correlationContext.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }
}
