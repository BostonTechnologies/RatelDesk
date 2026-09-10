using Helpdesk.Application.Events;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.DTOs.SlaPolicy;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Sla;

public static class SlaReportSubscriptionEndpoints
{
    public static void MapSlaReportSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sla-report-subscriptions")
            .WithTags("SLA Report Subscriptions")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async ([FromServices] IRepository<SlaReportSubscription> repo) =>
        {
            var all = await repo.GetAllAsync();
            return Results.Ok(all.OrderBy(x => x.TenantId).ThenBy(x => x.Id).Select(ToDto).ToList());
        });

        group.MapPost("/", async (
            [FromBody] SlaReportSubscriptionDto dto,
            [FromServices] IRepository<SlaReportSubscription> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            CancellationToken ct) =>
        {
            var entity = ToEntity(dto);
            var created = await repo.CreateAsync(entity);
            await domainEvents.PublishAsync(
                new SlaReportSubscriptionCreatedDomainEvent(
                    created.Id,
                    created.TenantId,
                    null,
                    null,
                    created.Targets.Count,
                    0,
                    DateTimeOffset.UtcNow,
                    GetCorrelationId(correlationContext)),
                ct);
            return Results.Created($"/api/v1/sla-report-subscriptions/{created.Id}", ToDto(created));
        });

        group.MapPut("/{id}", async (
            string id,
            [FromBody] SlaReportSubscriptionDto dto,
            [FromServices] IRepository<SlaReportSubscription> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            CancellationToken ct) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null)
            {
                return Results.NotFound();
            }

            ApplyUpdate(existing, dto);
            var updated = await repo.UpdateAsync(existing);
            if (updated is not null)
            {
                await domainEvents.PublishAsync(
                    new SlaReportSubscriptionUpdatedDomainEvent(
                        updated.Id,
                        updated.TenantId,
                        null,
                        null,
                        updated.Targets.Count,
                        0,
                        DateTimeOffset.UtcNow,
                        GetCorrelationId(correlationContext)),
                    ct);
            }

            return Results.Ok(ToDto(updated!));
        });

        group.MapDelete("/{id}", async (
            string id,
            [FromServices] IRepository<SlaReportSubscription> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            CancellationToken ct) =>
        {
            var existing = await repo.GetAsync(id);
            var deleted = await repo.DeleteAsync(id);
            if (!deleted)
            {
                return Results.NotFound();
            }

            if (existing is not null)
            {
                await domainEvents.PublishAsync(
                    new SlaReportSubscriptionDeletedDomainEvent(
                        existing.Id,
                        existing.TenantId,
                        null,
                        null,
                        existing.Targets.Count,
                        0,
                        DateTimeOffset.UtcNow,
                        GetCorrelationId(correlationContext)),
                    ct);
            }

            return Results.NoContent();
        });
    }

    private static string GetCorrelationId(ICorrelationContext correlationContext)
    {
        return correlationContext.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }

    private static SlaReportSubscriptionDto ToDto(SlaReportSubscription source)
    {
        return new SlaReportSubscriptionDto
        {
            Id = source.Id,
            TenantId = source.TenantId,
            IsActive = source.IsActive,
            Frequency = source.Frequency,
            WeeklyDay = source.WeeklyDay,
            SendTimeLocal = source.SendTimeLocal,
            TimeZoneId = source.TimeZoneId,
            Targets = source.Targets.Select(x => new RecipientTargetDto { Type = x.Type, Value = x.Value }).ToList(),
            LookbackDays = source.LookbackDays,
            IncludeCsvAttachment = source.IncludeCsvAttachment,
            IncludeExcelAttachment = source.IncludeExcelAttachment,
            TicketType = source.TicketType,
            ServiceId = source.ServiceId
        };
    }

    private static SlaReportSubscription ToEntity(SlaReportSubscriptionDto dto)
    {
        return new SlaReportSubscription
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? Dodo.Primitives.Uuid.CreateVersion7().ToString() : dto.Id,
            TenantId = dto.TenantId.Trim(),
            IsActive = dto.IsActive,
            Frequency = dto.Frequency,
            WeeklyDay = dto.WeeklyDay,
            SendTimeLocal = dto.SendTimeLocal,
            TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Africa/Johannesburg" : dto.TimeZoneId,
            Targets = dto.Targets
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => new RecipientTarget { Type = x.Type, Value = x.Value.Trim() })
                .ToList(),
            LookbackDays = Math.Max(1, dto.LookbackDays),
            IncludeCsvAttachment = dto.IncludeCsvAttachment,
            IncludeExcelAttachment = dto.IncludeExcelAttachment,
            TicketType = dto.TicketType,
            ServiceId = string.IsNullOrWhiteSpace(dto.ServiceId) ? null : dto.ServiceId.Trim()
        };
    }

    private static void ApplyUpdate(SlaReportSubscription existing, SlaReportSubscriptionDto dto)
    {
        existing.TenantId = dto.TenantId.Trim();
        existing.IsActive = dto.IsActive;
        existing.Frequency = dto.Frequency;
        existing.WeeklyDay = dto.WeeklyDay;
        existing.SendTimeLocal = dto.SendTimeLocal;
        existing.TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Africa/Johannesburg" : dto.TimeZoneId;
        existing.Targets = dto.Targets
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new RecipientTarget { Type = x.Type, Value = x.Value.Trim() })
            .ToList();
        existing.LookbackDays = Math.Max(1, dto.LookbackDays);
        existing.IncludeCsvAttachment = dto.IncludeCsvAttachment;
        existing.IncludeExcelAttachment = dto.IncludeExcelAttachment;
        existing.TicketType = dto.TicketType;
        existing.ServiceId = string.IsNullOrWhiteSpace(dto.ServiceId) ? null : dto.ServiceId.Trim();
    }
}
