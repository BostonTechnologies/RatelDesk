using Dodo.Primitives;
using Helpdesk.Application.Events;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs.SlaPolicy;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Sla;

public static class SlaPolicyEndpoints
{
    public static void MapSlaPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/slas")
            .WithTags("SLA")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async ([FromServices] ISlaPolicyRepository repo) =>
        {
            var policies = await repo.QueryWithEscalations()
                .OrderBy(x => x.ScopeType)
                .ThenBy(x => x.AppliesTo)
                .ThenBy(x => x.Name)
                .Select(x => ToDto(x))
                .ToListAsync();

            return Results.Ok(policies);
        })
        .WithName("GetSlaPolicies")
        .WithSummary("List SLA policies");

        group.MapPost("/", async (
            [FromBody] SlaPolicyDto dto,
            [FromServices] ISlaPolicyRepository repo,
            [FromServices] ISlaPolicyValidator validator,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var entity = ToEntity(dto, null);

            var validation = await ValidateSlaPolicyAsync(repo, validator, entity);
            if (validation is not null)
            {
                return validation;
            }

            var created = await repo.CreateAsync(entity);
            var createdWithEscalations = await repo.GetByIdWithEscalationsAsync(created.Id);
            var now = DateTimeOffset.UtcNow;
            var actor = GetActorId(httpContext);
            var correlationId = GetCorrelationId(correlationContext);
            await domainEvents.PublishAsync(
                new SlaPolicyCreatedDomainEvent(
                    created.Id,
                    created.TenantId,
                    created.ScopeType,
                    created.AppliesTo,
                    actor,
                    now,
                    correlationId),
                ct);
            if (created.IsActive)
            {
                await domainEvents.PublishAsync(
                    new SlaPolicyActivatedDomainEvent(
                        created.Id,
                        created.TenantId,
                        created.ScopeType,
                        created.AppliesTo,
                        actor,
                        now,
                        correlationId),
                    ct);
            }
            return Results.Created($"/api/v1/slas/{created.Id}", ToDto(createdWithEscalations ?? created));
        })
        .WithName("CreateSlaPolicy")
        .WithSummary("Create SLA policy");

        group.MapPut("/{id}", async (
            [FromRoute] string id,
            [FromBody] SlaPolicyDto dto,
            [FromServices] ISlaPolicyRepository repo,
            [FromServices] ISlaPolicyValidator validator,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await repo.GetByIdWithEscalationsAsync(id, ct);
            if (existing is null)
            {
                return Results.Problem("SLA policy not found", statusCode: 404);
            }

            var wasActive = existing.IsActive;
            ApplyUpdate(existing, dto);

            var validation = await ValidateSlaPolicyAsync(repo, validator, existing, existing.Id);
            if (validation is not null)
            {
                return validation;
            }

            var updated = await repo.UpdateAsync(existing);
            if (updated is not null)
            {
                var now = DateTimeOffset.UtcNow;
                var actor = GetActorId(httpContext);
                var correlationId = GetCorrelationId(correlationContext);
                await domainEvents.PublishAsync(
                    new SlaPolicyUpdatedDomainEvent(
                        updated.Id,
                        updated.TenantId,
                        updated.ScopeType,
                        updated.AppliesTo,
                        actor,
                        now,
                        correlationId),
                    ct);

                if (!wasActive && updated.IsActive)
                {
                    await domainEvents.PublishAsync(
                        new SlaPolicyActivatedDomainEvent(
                            updated.Id,
                            updated.TenantId,
                            updated.ScopeType,
                            updated.AppliesTo,
                            actor,
                            now,
                            correlationId),
                        ct);
                }

                if (wasActive && !updated.IsActive)
                {
                    await domainEvents.PublishAsync(
                        new SlaPolicyDeactivatedDomainEvent(
                            updated.Id,
                            updated.TenantId,
                            updated.ScopeType,
                            updated.AppliesTo,
                            actor,
                            now,
                            correlationId),
                        ct);
                }
            }

            return Results.Ok(ToDto(updated!));
        })
        .WithName("UpdateSlaPolicy")
        .WithSummary("Update SLA policy");

        group.MapDelete("/{id}", async (
            [FromRoute] string id,
            [FromServices] ISlaPolicyRepository repo,
            [FromServices] HelpdeskDbContext db,
            [FromServices] IDomainEventPublisher domainEvents,
            [FromServices] ICorrelationContext correlationContext,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var existing = await repo.GetByIdWithEscalationsAsync(id, ct);
            if (existing is null)
            {
                return Results.NotFound();
            }

            if (existing.ScopeType == SlaScopeType.SystemDefault && existing.IsActive)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["policy"] = ["Active system default policies cannot be deleted. Deactivate or replace first."]
                });
            }

            var isInUseByOpenTicket = await db.Tickets
                .AsNoTracking()
                .AnyAsync(t =>
                    t.SlaPolicyId == existing.Id &&
                    t.State != TicketState.Resolved, ct);
            if (isInUseByOpenTicket)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["policy"] = ["Policy is referenced by active tickets and cannot be deleted."]
                });
            }

            var wasActive = existing.IsActive;
            existing.IsActive = false;
            await repo.UpdateAsync(existing);
            var now = DateTimeOffset.UtcNow;
            var actor = GetActorId(httpContext);
            var correlationId = GetCorrelationId(correlationContext);
            if (wasActive)
            {
                await domainEvents.PublishAsync(
                    new SlaPolicyDeactivatedDomainEvent(
                        existing.Id,
                        existing.TenantId,
                        existing.ScopeType,
                        existing.AppliesTo,
                        actor,
                        now,
                        correlationId),
                    ct);
            }

            await domainEvents.PublishAsync(
                new SlaPolicyDeletedDomainEvent(
                    existing.Id,
                    existing.TenantId,
                    existing.ScopeType,
                    existing.AppliesTo,
                    actor,
                    now,
                    correlationId),
                ct);
            return Results.NoContent();
        })
        .WithName("DeleteSlaPolicy")
        .WithSummary("Soft delete SLA policy");
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

    private static SlaPolicyDto ToDto(SlaPolicy policy)
    {
        return new SlaPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            ScopeType = policy.ScopeType,
            TenantId = policy.TenantId,
            AppliesTo = policy.AppliesTo,
            Priority = policy.Priority,
            ServiceId = policy.ServiceId,
            MatchRank = policy.MatchRank,
            ResponseTimeHours = policy.ResponseTimeHours,
            ResolutionTimeHours = policy.ResolutionTimeHours,
            AutoResumeAfterHours = policy.AutoResumeAfterHours,
            IsActive = policy.IsActive,
            Escalations = policy.Escalations
                .OrderBy(x => x.Metric)
                .ThenBy(x => x.TriggerPercent)
                .Select(ToDto)
                .ToList()
        };
    }

    private static SlaEscalationRuleDto ToDto(SlaEscalationRule rule)
    {
        return new SlaEscalationRuleDto
        {
            Id = rule.Id,
            Metric = rule.Metric,
            TriggerPercent = rule.TriggerPercent,
            Recipients = rule.Recipients.ToList(),
            Targets = rule.Targets
                .Select(x => new RecipientTargetDto
                {
                    Type = x.Type,
                    Value = x.Value
                })
                .ToList(),
            IsActive = rule.IsActive,
            Note = rule.Note
        };
    }

    private static SlaPolicy ToEntity(SlaPolicyDto dto, string? existingId)
    {
        return new SlaPolicy
        {
            Id = string.IsNullOrWhiteSpace(existingId)
                ? (string.IsNullOrWhiteSpace(dto.Id) ? Uuid.CreateVersion7().ToString() : dto.Id)
                : existingId,
            Name = dto.Name,
            Description = dto.Description,
            ScopeType = dto.ScopeType,
            TenantId = NormalizeTenantId(dto.ScopeType, dto.TenantId),
            AppliesTo = dto.AppliesTo,
            Priority = NormalizePriority(dto.ScopeType, dto.Priority),
            ServiceId = NormalizeServiceId(dto.ScopeType, dto.ServiceId),
            MatchRank = dto.MatchRank,
            ResponseTimeHours = dto.ResponseTimeHours,
            ResolutionTimeHours = dto.ResolutionTimeHours,
            AutoResumeAfterHours = NormalizeAutoResumeAfterHours(dto.ScopeType, dto.AutoResumeAfterHours),
            IsActive = dto.IsActive,
            Escalations = dto.Escalations.Select(ToEntity).ToList()
        };
    }

    private static SlaEscalationRule ToEntity(SlaEscalationRuleDto dto)
    {
        return new SlaEscalationRule
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? Uuid.CreateVersion7().ToString() : dto.Id,
            Metric = dto.Metric,
            TriggerPercent = dto.TriggerPercent,
            Recipients = NormalizeRecipients(dto.Recipients),
            Targets = NormalizeTargets(dto.Targets, dto.Recipients),
            IsActive = dto.IsActive,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        };
    }

    private static void ApplyUpdate(SlaPolicy existing, SlaPolicyDto dto)
    {
        existing.Name = dto.Name;
        existing.Description = dto.Description;
        existing.ScopeType = dto.ScopeType;
        existing.TenantId = NormalizeTenantId(dto.ScopeType, dto.TenantId);
        existing.AppliesTo = dto.AppliesTo;
        existing.Priority = NormalizePriority(dto.ScopeType, dto.Priority);
        existing.ServiceId = NormalizeServiceId(dto.ScopeType, dto.ServiceId);
        existing.MatchRank = dto.MatchRank;
        existing.ResponseTimeHours = dto.ResponseTimeHours;
        existing.ResolutionTimeHours = dto.ResolutionTimeHours;
        existing.AutoResumeAfterHours = NormalizeAutoResumeAfterHours(dto.ScopeType, dto.AutoResumeAfterHours);
        existing.IsActive = dto.IsActive;

        var incoming = dto.Escalations.Select(ToEntity).ToList();
        foreach (var rule in incoming)
        {
            rule.PolicyId = existing.Id;
        }

        existing.Escalations.Clear();
        foreach (var rule in incoming)
        {
            existing.Escalations.Add(rule);
        }
    }

    private static async Task<IResult?> ValidateSlaPolicyAsync(
        ISlaPolicyRepository repo,
        ISlaPolicyValidator validator,
        SlaPolicy policy,
        string? currentId = null)
    {
        try
        {
            validator.ValidateForSave(policy);
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["policy"] = [ex.Message]
            });
        }

        if (policy.ScopeType == SlaScopeType.SystemDefault && policy.IsActive)
        {
            var hasConflict = await repo.Query().AnyAsync(x =>
                x.ScopeType == SlaScopeType.SystemDefault &&
                x.AppliesTo == policy.AppliesTo &&
                x.IsActive &&
                x.Id != currentId);

            if (hasConflict)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["scopeType"] = ["Only one active SystemDefault policy is allowed per TicketType."]
                });
            }
        }

        return null;
    }

    private static List<string> NormalizeRecipients(List<string>? recipients)
    {
        if (recipients is null)
        {
            return new List<string>();
        }

        return recipients
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int? NormalizeAutoResumeAfterHours(SlaScopeType scopeType, int? autoResumeAfterHours)
    {
        return scopeType == SlaScopeType.SystemDefault ? null : autoResumeAfterHours;
    }

    private static string? NormalizeServiceId(SlaScopeType scopeType, string? serviceId)
    {
        if (scopeType == SlaScopeType.SystemDefault)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(serviceId) ? null : serviceId.Trim();
    }

    private static int? NormalizePriority(SlaScopeType scopeType, int? priority)
    {
        return scopeType == SlaScopeType.SystemDefault ? null : priority;
    }

    private static List<RecipientTarget> NormalizeTargets(List<RecipientTargetDto>? targets, List<string>? recipients)
    {
        var mappedTargets = (targets ?? new List<RecipientTargetDto>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new RecipientTarget
            {
                Type = x.Type,
                Value = x.Value.Trim()
            })
            .ToList();
        if (mappedTargets.Count > 0)
        {
            return mappedTargets;
        }

        return NormalizeRecipients(recipients)
            .Select(x => new RecipientTarget
            {
                Type = RecipientTargetType.Email,
                Value = x
            })
            .ToList();
    }

    private static string? NormalizeTenantId(SlaScopeType scopeType, string? tenantId)
    {
        return scopeType == SlaScopeType.SystemDefault ? null : tenantId?.Trim();
    }
}
