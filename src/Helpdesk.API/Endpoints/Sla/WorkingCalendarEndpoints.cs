using Helpdesk.Application.Events;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Sla;

public static class WorkingCalendarEndpoints
{
    public static void MapWorkingCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sla-calendars")
            .WithTags("SLA Calendars")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async ([FromServices] IRepository<WorkingCalendar> repo) =>
        {
            var all = await repo.GetAllAsync();
            var dtos = all
                .OrderBy(x => x.ScopeType)
                .ThenBy(x => x.TenantId)
                .ThenBy(x => x.Name)
                .Select(ToDto)
                .ToList();
            return Results.Ok(dtos);
        });

        group.MapPost("/", async (
            [FromBody] WorkingCalendarDto dto,
            [FromServices] IRepository<WorkingCalendar> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var entity = ToEntity(dto);
            await EnsureSingleActiveCalendarAsync(entity, repo);
            var created = await repo.CreateAsync(entity);
            var now = DateTimeOffset.UtcNow;
            var actor = GetActorId(httpContext);
            var correlationId = GetCorrelationId(correlationContext);
            await domainEvents.PublishAsync(
                new WorkingCalendarCreatedDomainEvent(
                    created.Id,
                    created.TenantId,
                    created.TimeZoneId,
                    actor,
                    now,
                    correlationId),
                ct);
            if (created.IsActive)
            {
                await domainEvents.PublishAsync(
                    new WorkingCalendarActivatedDomainEvent(
                        created.Id,
                        created.TenantId,
                        created.TimeZoneId,
                        actor,
                        now,
                        correlationId),
                    ct);
            }
            return Results.Created($"/api/v1/sla-calendars/{created.Id}", ToDto(created));
        });

        group.MapPut("/{id}", async (
            string id,
            [FromBody] WorkingCalendarDto dto,
            [FromServices] IRepository<WorkingCalendar> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var wasActive = existing.IsActive;
            ApplyUpdate(existing, dto);
            await EnsureSingleActiveCalendarAsync(existing, repo, existing.Id);
            var updated = await repo.UpdateAsync(existing);
            if (updated is not null)
            {
                var now = DateTimeOffset.UtcNow;
                var actor = GetActorId(httpContext);
                var correlationId = GetCorrelationId(correlationContext);
                await domainEvents.PublishAsync(
                    new WorkingCalendarUpdatedDomainEvent(
                        updated.Id,
                        updated.TenantId,
                        updated.TimeZoneId,
                        actor,
                        now,
                        correlationId),
                    ct);
                if (!wasActive && updated.IsActive)
                {
                    await domainEvents.PublishAsync(
                        new WorkingCalendarActivatedDomainEvent(
                            updated.Id,
                            updated.TenantId,
                            updated.TimeZoneId,
                            actor,
                            now,
                            correlationId),
                        ct);
                }
            }

            return Results.Ok(ToDto(updated!));
        });

        group.MapDelete("/{id}", async (
            string id,
            [FromServices] IRepository<WorkingCalendar> repo,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
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
                    new WorkingCalendarDeletedDomainEvent(
                        existing.Id,
                        existing.TenantId,
                        existing.TimeZoneId,
                        GetActorId(httpContext),
                        DateTimeOffset.UtcNow,
                        GetCorrelationId(correlationContext)),
                    ct);
            }

            return Results.NoContent();
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

    private static WorkingCalendarDto ToDto(WorkingCalendar calendar)
    {
        return new WorkingCalendarDto
        {
            Id = calendar.Id,
            ScopeType = calendar.ScopeType,
            TenantId = calendar.TenantId,
            Name = calendar.Name,
            TimeZoneId = calendar.TimeZoneId,
            IsActive = calendar.IsActive,
            WeeklyRules = calendar.WeeklyRules.Select(x => new WorkingDayRuleDto
            {
                DayOfWeek = x.DayOfWeek,
                IsWorkingDay = x.IsWorkingDay,
                WorkingHours = x.WorkingHours.Select(y => new TimeRangeDto { Start = y.Start, End = y.End }).ToList()
            }).ToList(),
            Exceptions = calendar.Exceptions.Select(x => new CalendarExceptionDto
            {
                Date = x.Date,
                IsWorkingDayOverride = x.IsWorkingDayOverride,
                WorkingHoursOverride = x.WorkingHoursOverride.Select(y => new TimeRangeDto { Start = y.Start, End = y.End }).ToList(),
                Note = x.Note
            }).ToList()
        };
    }

    private static WorkingCalendar ToEntity(WorkingCalendarDto dto)
    {
        var calendar = new WorkingCalendar
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? Dodo.Primitives.Uuid.CreateVersion7().ToString() : dto.Id,
            ScopeType = dto.ScopeType,
            TenantId = dto.ScopeType == CalendarScopeType.SystemDefault ? null : dto.TenantId?.Trim(),
            Name = dto.Name,
            TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Africa/Johannesburg" : dto.TimeZoneId,
            IsActive = dto.IsActive,
            WeeklyRules = NormalizeWeeklyRules(dto.WeeklyRules),
            Exceptions = dto.Exceptions.Select(x => new CalendarException
            {
                Date = x.Date,
                IsWorkingDayOverride = x.IsWorkingDayOverride,
                WorkingHoursOverride = x.WorkingHoursOverride.Select(y => new TimeRange { Start = y.Start, End = y.End }).ToList(),
                Note = string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim()
            }).ToList()
        };

        return calendar;
    }

    private static void ApplyUpdate(WorkingCalendar existing, WorkingCalendarDto dto)
    {
        existing.ScopeType = dto.ScopeType;
        existing.TenantId = dto.ScopeType == CalendarScopeType.SystemDefault ? null : dto.TenantId?.Trim();
        existing.Name = dto.Name;
        existing.TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Africa/Johannesburg" : dto.TimeZoneId;
        existing.IsActive = dto.IsActive;
        existing.WeeklyRules = NormalizeWeeklyRules(dto.WeeklyRules);
        existing.Exceptions = dto.Exceptions.Select(x => new CalendarException
        {
            Date = x.Date,
            IsWorkingDayOverride = x.IsWorkingDayOverride,
            WorkingHoursOverride = x.WorkingHoursOverride.Select(y => new TimeRange { Start = y.Start, End = y.End }).ToList(),
            Note = string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim()
        }).ToList();
    }

    private static List<WorkingDayRule> NormalizeWeeklyRules(List<WorkingDayRuleDto> rules)
    {
        var mapped = rules
            .GroupBy(x => x.DayOfWeek)
            .Select(g => g.First())
            .Where(x => x.DayOfWeek >= 0 && x.DayOfWeek <= 6)
            .Select(x => new WorkingDayRule
            {
                DayOfWeek = x.DayOfWeek,
                IsWorkingDay = x.IsWorkingDay,
                WorkingHours = x.WorkingHours
                    .Where(y => y.End > y.Start)
                    .Select(y => new TimeRange { Start = y.Start, End = y.End })
                    .OrderBy(y => y.Start)
                    .ToList()
            })
            .ToDictionary(x => x.DayOfWeek, x => x);

        for (var day = 0; day <= 6; day++)
        {
            if (!mapped.ContainsKey(day))
            {
                mapped[day] = new WorkingDayRule
                {
                    DayOfWeek = day,
                    IsWorkingDay = false,
                    WorkingHours = new List<TimeRange>()
                };
            }
        }

        return mapped.Values.OrderBy(x => x.DayOfWeek).ToList();
    }

    private static async Task EnsureSingleActiveCalendarAsync(WorkingCalendar calendar, IRepository<WorkingCalendar> repo, string? currentId = null)
    {
        if (!calendar.IsActive)
        {
            return;
        }

        var all = await repo.GetAllAsync();
        foreach (var existing in all)
        {
            if (existing.Id == currentId)
            {
                continue;
            }

            if (!existing.IsActive)
            {
                continue;
            }

            if (calendar.ScopeType == CalendarScopeType.SystemDefault && existing.ScopeType == CalendarScopeType.SystemDefault)
            {
                existing.IsActive = false;
                await repo.UpdateAsync(existing);
            }

            if (calendar.ScopeType == CalendarScopeType.Tenant &&
                existing.ScopeType == CalendarScopeType.Tenant &&
                string.Equals(existing.TenantId, calendar.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                existing.IsActive = false;
                await repo.UpdateAsync(existing);
            }
        }
    }
}
