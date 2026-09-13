using FluentValidation;
using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Organization;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Organization;

public static class OrganizationAiKbSettingsEndpoints
{
    public static void MapOrganizationAiKbSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organizations/{id}/ai-kb-settings")
            .WithTags("Organizations")
            .RequireAuthorization("HelpdeskAdmin");
        var operatorGroup = app.MapGroup("/api/v1/organizations/{id}/ai-kb-settings")
            .WithTags("Organizations")
            .RequireAuthorization(policy => policy.RequireAssertion(ctx =>
                ctx.User.Claims.Any(c =>
                    (c.Type == "roles" || c.Type == ClaimTypes.Role) &&
                    (string.Equals(c.Value, "HelpdeskAdmin", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(c.Value, "Technician", StringComparison.OrdinalIgnoreCase)))));

        group.MapGet("/", async (string id, IRepository<OrganizationAiKbSettings> repo) =>
        {
            var settings = (await repo.GetAllAsync()).FirstOrDefault(s => s.OrganizationId == id);
            if (settings is null) return Results.NotFound();

            var dto = new OrganizationAiKbSettingsDto
            {
                EnableAiSearch = settings.EnableAiSearch,
                EnableAiAnswers = settings.EnableAiAnswers,
                SearchThreshold = settings.SearchThreshold,
                AnswerThreshold = settings.AnswerThreshold,
                EmbeddingProviderId = settings.EmbeddingProviderId,
                EmbeddingModel = settings.EmbeddingModel,
                EmbeddingDimensions = settings.EmbeddingDimensions,
                KnowledgeProviderId = settings.KnowledgeProviderId,
                KnowledgeModelName = settings.KnowledgeModelName,
                SuggestionLimit = settings.SuggestionLimit,
                AllowedServicesCsv = settings.AllowedServicesCsv,
                EnableProviderFallback = settings.EnableProviderFallback,
                MaxProviderAttempts = settings.MaxProviderAttempts,
                MinimumSuggestionFeedbackCount = settings.MinimumSuggestionFeedbackCount,
                MinimumSuggestionHelpfulRate = settings.MinimumSuggestionHelpfulRate,
                MinimumAutomationFeedbackCount = settings.MinimumAutomationFeedbackCount,
                MinimumAutomationResolvedRate = settings.MinimumAutomationResolvedRate
            };

            return Results.Ok(dto);
        })
        .WithName("GetOrganizationAiKbSettings")
        .WithSummary("Get AI KB settings for an organization");

        operatorGroup.MapGet("/readiness", async (string id, HelpdeskDbContext db) =>
        {
            var settings = await db.OrganizationAiKbSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == id);
            if (settings is null) return Results.NotFound();

            var ticketIds = db.Tickets
                .AsNoTracking()
                .Where(t => t.OrganizationId == id)
                .Select(t => t.Id);

            var suggestionFeedbackCount = await db.TicketAiFeedback
                .AsNoTracking()
                .Where(x => x.FeedbackType == "suggestion" && ticketIds.Contains(x.TicketId))
                .CountAsync();
            var helpfulSuggestionCount = await db.TicketAiFeedback
                .AsNoTracking()
                .Where(x => x.FeedbackType == "suggestion" && x.FeedbackValue == "helpful" && ticketIds.Contains(x.TicketId))
                .CountAsync();
            var suggestionHelpfulRate = suggestionFeedbackCount == 0 ? 0 : (double)helpfulSuggestionCount / suggestionFeedbackCount;

            var automationFeedbackCount = await db.TicketAiFeedback
                .AsNoTracking()
                .Where(x => x.FeedbackType == "automation" && ticketIds.Contains(x.TicketId))
                .CountAsync();
            var automationResolvedCount = await db.TicketAiFeedback
                .AsNoTracking()
                .Where(x => x.FeedbackType == "automation" && x.FeedbackValue == "resolved" && ticketIds.Contains(x.TicketId))
                .CountAsync();
            var automationResolvedRate = automationFeedbackCount == 0 ? 0 : (double)automationResolvedCount / automationFeedbackCount;

            var aiAuditCount = await db.AiOperationAuditRecords
                .AsNoTracking()
                .Where(x => x.OrganizationId == id)
                .CountAsync();
            var lastAiAuditAt = await db.AiOperationAuditRecords
                .AsNoTracking()
                .Where(x => x.OrganizationId == id)
                .OrderByUtc(db, x => x.CreatedAt, descending: true)
                .Select(x => (DateTimeOffset?)x.CreatedAt)
                .FirstOrDefaultAsync();

            var blockingReasons = new List<string>();
            var suggestionGateMet =
                suggestionFeedbackCount >= settings.MinimumSuggestionFeedbackCount &&
                suggestionHelpfulRate >= settings.MinimumSuggestionHelpfulRate;
            if (!suggestionGateMet)
            {
                blockingReasons.Add(
                    $"Suggestion quality gate not met ({suggestionFeedbackCount}/{settings.MinimumSuggestionFeedbackCount} feedback, {suggestionHelpfulRate:P0}/{settings.MinimumSuggestionHelpfulRate:P0} helpful rate).");
            }

            var automationGateMet =
                automationFeedbackCount >= settings.MinimumAutomationFeedbackCount &&
                automationResolvedRate >= settings.MinimumAutomationResolvedRate;
            if (!automationGateMet)
            {
                blockingReasons.Add(
                    $"Automation quality gate not met ({automationFeedbackCount}/{settings.MinimumAutomationFeedbackCount} feedback, {automationResolvedRate:P0}/{settings.MinimumAutomationResolvedRate:P0} resolved rate).");
            }

            return Results.Ok(new OrganizationAiReadinessDto
            {
                SuggestionFeedbackCount = suggestionFeedbackCount,
                HelpfulSuggestionCount = helpfulSuggestionCount,
                SuggestionHelpfulRate = suggestionHelpfulRate,
                AutomationFeedbackCount = automationFeedbackCount,
                AutomationResolvedCount = automationResolvedCount,
                AutomationResolvedRate = automationResolvedRate,
                SuggestionGateMet = suggestionGateMet,
                AutomationGateMet = automationGateMet,
                ProductionReady = suggestionGateMet && automationGateMet,
                BlockingReasons = blockingReasons,
                AiAuditCount = aiAuditCount,
                LastAiAuditAt = lastAiAuditAt
            });
        })
        .WithName("GetOrganizationAiKbReadiness")
        .WithSummary("Get AI rollout readiness for an organization");

        operatorGroup.MapGet("/runtime-status", async (
            string id,
            [FromQuery] int timeoutMs,
            HelpdeskDbContext db,
            IAiProviderService providerService,
            CancellationToken token) =>
        {
            var providerTimeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs <= 0 ? 1500 : timeoutMs, 250, 5000));
            var settings = await db.OrganizationAiKbSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == id, token);
            if (settings is null) return Results.NotFound();

            var enabledProviders = await db.AiProviders
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .CountAsync(token);

            var providerStatuses = new List<OrganizationAiRuntimeProviderStatusDto>();
            var runtimeReasons = new List<string>();
            var recentRuntimeRecords = await db.AiOperationAuditRecords
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == id &&
                    (x.OperationName == "runtime-chat-fallback" || x.OperationName == "runtime-chat-failure"))
                .OrderByUtc(db, x => x.CreatedAt, descending: true)
                .Take(10)
                .ToListAsync(token);

            if (settings.EnableAiSearch)
            {
                providerStatuses.Add(await BuildProviderStatusAsync(
                    "Search",
                    settings.EmbeddingProviderId,
                    settings.EmbeddingModel,
                    providerService,
                    providerTimeout,
                    token));
            }

            if (settings.EnableAiAnswers)
            {
                providerStatuses.Add(await BuildProviderStatusAsync(
                    "Answers",
                    settings.KnowledgeProviderId,
                    settings.KnowledgeModelName,
                    providerService,
                    providerTimeout,
                    token));
            }

            foreach (var providerStatus in providerStatuses.Where(x => !string.Equals(x.Status, "Ready", StringComparison.OrdinalIgnoreCase)))
            {
                runtimeReasons.AddRange(providerStatus.Reasons.Select(reason => $"{providerStatus.Channel}: {reason}"));
            }

            var fallbackProviderCount = Math.Max(0, enabledProviders - providerStatuses
                .Where(x => !string.IsNullOrWhiteSpace(x.ProviderId) && x.Enabled)
                .Select(x => x.ProviderId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

            var requiredChannelCount = providerStatuses.Count;
            var readyChannelCount = providerStatuses.Count(x => string.Equals(x.Status, "Ready", StringComparison.OrdinalIgnoreCase));
            var hasDegradedChannel = providerStatuses.Any(x => string.Equals(x.Status, "Degraded", StringComparison.OrdinalIgnoreCase));

            var overallStatus = requiredChannelCount == 0
                ? "Disabled"
                : readyChannelCount == requiredChannelCount
                    ? "Ready"
                    : hasDegradedChannel || (settings.EnableProviderFallback && fallbackProviderCount > 0)
                        ? "Degraded"
                        : "Unavailable";

            if (requiredChannelCount == 0)
            {
                runtimeReasons.Add("AI search and AI answers are both disabled for this organization.");
            }
            else if (!runtimeReasons.Any() && string.Equals(overallStatus, "Degraded", StringComparison.OrdinalIgnoreCase))
            {
                runtimeReasons.Add("Configured runtime is degraded, but fallback capacity exists.");
            }

            foreach (var providerStatus in providerStatuses)
            {
                providerStatus.RecentEvidence = recentRuntimeRecords
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.ProviderName) && string.Equals(x.ProviderName, providerStatus.ProviderName, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.ProviderName)
                            && !string.IsNullOrWhiteSpace(providerStatus.ProviderName)
                            && x.ProviderName.Contains(providerStatus.ProviderName, StringComparison.OrdinalIgnoreCase)))
                    .Take(3)
                    .Select(x => $"{x.CreatedAt.LocalDateTime:g}: {x.OperationName} - {x.Notes}")
                    .ToList();
            }

            return Results.Ok(new OrganizationAiRuntimeStatusDto
            {
                Status = overallStatus,
                RuntimeReady = string.Equals(overallStatus, "Ready", StringComparison.OrdinalIgnoreCase),
                FallbackEnabled = settings.EnableProviderFallback,
                MaxProviderAttempts = settings.MaxProviderAttempts,
                EnabledProviderCount = enabledProviders,
                FallbackProviderCount = fallbackProviderCount,
                Reasons = runtimeReasons,
                RecentEvidence = recentRuntimeRecords
                    .Take(5)
                    .Select(x => $"{x.CreatedAt.LocalDateTime:g}: {x.OperationName} - {x.Notes}")
                    .ToList(),
                Providers = providerStatuses
            });
        })
        .WithName("GetOrganizationAiKbRuntimeStatus")
        .WithSummary("Get AI provider/runtime status for an organization");

        group.MapPut("/", async (string id, OrganizationAiKbSettingsDto dto, IRepository<OrganizationAiKbSettings> repo, IValidator<OrganizationAiKbSettingsDto> validator) =>
        {
            dto.EmbeddingModel = NormalizeEmbeddingModel(dto.EmbeddingModel);
            dto.EmbeddingDimensions = dto.EmbeddingDimensions > 0 ? dto.EmbeddingDimensions : 768;
            dto.SuggestionLimit = dto.SuggestionLimit > 0 ? dto.SuggestionLimit : 3;
            dto.MaxProviderAttempts = dto.MaxProviderAttempts > 0 ? dto.MaxProviderAttempts : 2;

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            var settings = (await repo.GetAllAsync()).FirstOrDefault(s => s.OrganizationId == id);
            if (settings is null) return Results.NotFound();

            settings.EnableAiSearch = dto.EnableAiSearch;
            settings.EnableAiAnswers = dto.EnableAiAnswers;
            settings.SearchThreshold = dto.SearchThreshold;
            settings.AnswerThreshold = dto.AnswerThreshold;
            settings.EmbeddingProviderId = dto.EmbeddingProviderId;
            settings.EmbeddingModel = dto.EmbeddingModel;
            settings.EmbeddingDimensions = dto.EmbeddingDimensions;
            settings.KnowledgeProviderId = dto.KnowledgeProviderId;
            settings.KnowledgeModelName = dto.KnowledgeModelName;
            settings.SuggestionLimit = dto.SuggestionLimit;
            settings.AllowedServicesCsv = dto.AllowedServicesCsv;
            settings.EnableProviderFallback = dto.EnableProviderFallback;
            settings.MaxProviderAttempts = dto.MaxProviderAttempts;
            settings.MinimumSuggestionFeedbackCount = dto.MinimumSuggestionFeedbackCount;
            settings.MinimumSuggestionHelpfulRate = dto.MinimumSuggestionHelpfulRate;
            settings.MinimumAutomationFeedbackCount = dto.MinimumAutomationFeedbackCount;
            settings.MinimumAutomationResolvedRate = dto.MinimumAutomationResolvedRate;

            await repo.UpdateAsync(settings);
            return Results.Ok(dto);
        })
        .WithName("UpdateOrganizationAiKbSettings")
        .WithSummary("Update AI KB settings for an organization");
    }

    private static string NormalizeEmbeddingModel(string? embeddingModel)
        => string.IsNullOrWhiteSpace(embeddingModel) ? "embeddinggemma" : embeddingModel.Trim();

    private static async Task<OrganizationAiRuntimeProviderStatusDto> BuildProviderStatusAsync(
        string channel,
        string? providerId,
        string? modelId,
        IAiProviderService providerService,
        TimeSpan providerTimeout,
        CancellationToken token)
    {
        var dto = new OrganizationAiRuntimeProviderStatusDto
        {
            Channel = channel,
            ProviderId = providerId,
            ModelId = modelId
        };

        if (string.IsNullOrWhiteSpace(providerId))
        {
            dto.Status = "Unavailable";
            dto.Reasons.Add("No provider is configured.");
            return dto;
        }

        if (!Guid.TryParse(providerId, out var parsedProviderId))
        {
            dto.Status = "Unavailable";
            dto.Reasons.Add("Configured provider id is invalid.");
            return dto;
        }

        var provider = await providerService.GetAsync(parsedProviderId, token);
        if (provider is null)
        {
            dto.Status = "Unavailable";
            dto.Reasons.Add("Configured provider no longer exists.");
            return dto;
        }

        dto.ProviderName = provider.Name;
        dto.Enabled = provider.IsEnabled;
        dto.Configured = true;

        if (!provider.IsEnabled)
        {
            dto.Status = "Unavailable";
            dto.Reasons.Add("Configured provider is disabled.");
            return dto;
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            dto.Status = "Unavailable";
            dto.Reasons.Add("No model is configured.");
            return dto;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(providerTimeout);
            var result = await providerService.TestAsync(parsedProviderId, timeoutCts.Token);
            dto.ConnectivityOk = result.Success;
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            {
                dto.Reasons.Add(result.Message);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            dto.ConnectivityOk = false;
            dto.Reasons.Add($"Connectivity test timed out after {(int)providerTimeout.TotalMilliseconds}ms.");
        }
        catch
        {
            dto.ConnectivityOk = false;
        }

        if (dto.ConnectivityOk)
        {
            dto.Status = "Ready";
            return dto;
        }

        dto.Status = "Degraded";
        if (!dto.Reasons.Any())
        {
            dto.Reasons.Add("Connectivity test failed.");
        }
        return dto;
    }
}
