using Helpdesk.Application.Services.KB;
using Helpdesk.Application.Orchestration;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.DTOs.Orchestration;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Services.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.KB;

public static class KBEndpoints
{
    public static void MapKbEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kb")
            .WithTags("Knowledge Base")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async (
            [FromServices] HelpdeskDbContext db,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] KnowledgeBaseArticleState? state,
            [FromQuery] string? search,
            CancellationToken token) =>
        {
            var query = db.KnowledgeBaseArticles.AsNoTracking();

            if (state.HasValue)
            {
                query = query.Where(a => a.State == state.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var like = $"%{search}%";
                query = query.Where(a => EF.Functions.Like(a.Title, like) ||
                                          (a.Summary != null && EF.Functions.Like(a.Summary, like)));
            }

            var totalCount = await query.CountAsync(token);
            var effectivePage = page ?? 1;
            var effectivePageSize = pageSize ?? 10;
            var items = await query
                .OrderByDescending(a => a.PublishedAt ?? a.UpdatedAt ?? a.CreatedAt)
                .Skip((effectivePage - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .Select(a => new ArticleDto
                {
                    Id = a.Id.ToString(),
                    Title = a.Title,
                    Content = a.Summary,
                    CategoryId = a.Service,
                    IsPublic = a.IsPublished,
                    State = a.State,
                    LinkedTicketId = a.LinkedTicketId,
                    CreatedAt = a.CreatedAt,
                    AutomationTarget = string.IsNullOrWhiteSpace(a.AutomationBindingId)
                        && string.IsNullOrWhiteSpace(a.AutomationRequestFormId)
                        && a.AutomationTaskTemplateId == null
                        ? null
                        : new KnowledgeArticleAutomationTargetDto
                        {
                            BindingId = a.AutomationBindingId,
                            RequestFormId = a.AutomationRequestFormId,
                            TaskTemplateId = a.AutomationTaskTemplateId,
                            TaskTemplateName = a.AutomationTaskTemplateName,
                            OrchestrationRequestDefinitionId = a.AutomationOrchestrationRequestDefinitionId,
                            OrchestrationJobDefinitionId = a.AutomationOrchestrationJobDefinitionId,
                            Enabled = !string.IsNullOrWhiteSpace(a.AutomationBindingId)
                        }
                })
                .ToListAsync(token);

            return Results.Ok(new PagedResponse<ArticleDto>
            {
                Items = items,
                Page = effectivePage,
                PageSize = effectivePageSize,
                TotalCount = totalCount
            });
        })
        .WithName("GetKnowledgeBase")
        .WithSummary("List knowledge base articles");

        group.MapGet("/{id}", async (
            [FromRoute] Guid id,
            [FromServices] HelpdeskDbContext db,
            CancellationToken token) =>
        {
            var article = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, token);
            if (article is null) return Results.NotFound();

            var articleChunks = await db.KnowledgeEmbeddings
                .AsNoTracking()
                .Where(e => e.KnowledgeBaseArticleId == id)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(token);

            var dto = new ArticleDto
            {
                Id = article.Id.ToString(),
                Title = article.Title,
                Content = article.Summary,
                CategoryId = article.Service,
                IsPublic = article.IsPublished,
                State = article.State,
                LinkedTicketId = article.LinkedTicketId,
                CreatedAt = article.CreatedAt,
                AutomationTarget = MapAutomationTarget(article, false)
            };

            if (!string.IsNullOrWhiteSpace(article.AutomationBindingId))
            {
                var binding = await db.AutomationBindings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == article.AutomationBindingId, token);
                if (binding is not null)
                {
                    dto.AutomationTarget = MapAutomationTarget(article, binding.Enabled);
                }
            }

            var stats = new KnowledgeBaseArticleStatsDto
            {
                ChunkCount = articleChunks.Count,
                SourceType = articleChunks.FirstOrDefault()?.SourceType ?? "KB",
                SourceId = articleChunks.FirstOrDefault()?.SourceId ?? article.Id.ToString(),
                DocumentTitle = articleChunks.FirstOrDefault()?.DocumentTitle ?? article.Title,
                LastIndexedAt = articleChunks.FirstOrDefault()?.CreatedAt
            };

            var articleIdText = article.Id.ToString();
            var linkedTicketId = article.LinkedTicketId;
            var auditEntries = await db.AiOperationAuditRecords
                .AsNoTracking()
                .Where(x =>
                    (x.SubjectId == articleIdText && x.OperationName == "kb-build") ||
                    (!string.IsNullOrWhiteSpace(linkedTicketId) &&
                     x.SubjectId == linkedTicketId &&
                     x.OperationName == "kb-draft-from-ticket"))
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new KnowledgeArticleAiAuditEntryDto
                {
                    Id = x.Id,
                    OperationName = x.OperationName,
                    ProviderName = x.ProviderName,
                    ModelId = x.ModelId,
                    CorrelationId = x.CorrelationId,
                    Notes = x.Notes,
                    SourceSubjectId = x.SubjectId,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(token);

            return Results.Ok(new { Article = dto, Stats = stats, AuditEntries = auditEntries });
        })
        .WithName("GetKnowledgeBaseArticle")
        .WithSummary("Get knowledge base article details");

        group.MapPut("/{id}/state", async (
            [FromRoute] Guid id,
            [FromBody] UpdateKbStateDto dto,
            [FromServices] IRepository<KnowledgeBaseArticle> repo,
            CancellationToken token) =>
        {
            var article = await repo.GetAsync(id.ToString());
            if (article is null) return Results.NotFound();

            article.State = dto.State;
            article.IsPublished = dto.State == KnowledgeBaseArticleState.Published;
            article.PublishedAt = article.IsPublished ? DateTime.UtcNow : null;
            article.UpdatedAt = DateTime.UtcNow;

            var updated = await repo.UpdateAsync(article);
            return Results.Ok(updated);
        })
        .WithName("UpdateKnowledgeBaseState")
        .WithSummary("Update article state");

        group.MapPut("/{id}/automation-target", async (
            [FromRoute] Guid id,
            [FromBody] UpdateKnowledgeArticleAutomationTargetDto dto,
            [FromServices] HelpdeskDbContext db,
            [FromServices] IAutomationBindingService automationBindings,
            CancellationToken token) =>
        {
            var article = await db.KnowledgeBaseArticles
                .FirstOrDefaultAsync(x => x.Id == id, token);
            if (article is null)
            {
                return Results.NotFound();
            }

            if (dto.ClearTarget)
            {
                ClearAutomationTarget(article);
                article.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(token);
                return Results.Ok(MapAutomationTarget(article, false));
            }

            if (string.IsNullOrWhiteSpace(dto.BindingId))
            {
                return Results.BadRequest("BindingId is required when assigning an automation target.");
            }

            var binding = await automationBindings.GetAsync(dto.BindingId.Trim(), token);
            if (binding is null)
            {
                return Results.NotFound();
            }

            if (!binding.Enabled)
            {
                return Results.BadRequest("The selected automation binding is disabled.");
            }

            if (!string.Equals(article.OrganizationId, binding.OrganizationId, StringComparison.Ordinal))
            {
                return Results.BadRequest("The selected automation binding belongs to a different organization.");
            }

            article.AutomationBindingId = binding.Id;
            article.AutomationRequestFormId = binding.RequestFormId;
            article.AutomationTaskTemplateId = binding.TaskTemplateId;
            article.AutomationTaskTemplateName = binding.TaskTemplateName;
            article.AutomationOrchestrationRequestDefinitionId = binding.OrchestrationRequestDefinitionId;
            article.AutomationOrchestrationJobDefinitionId = binding.OrchestrationJobDefinitionId;
            article.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(token);

            return Results.Ok(MapAutomationTarget(article, binding.Enabled));
        })
        .WithName("UpdateKnowledgeBaseArticleAutomationTarget")
        .WithSummary("Assign or clear the External orchestration automation target for a knowledge article");

        group.MapDelete("/{id}", async (
            [FromRoute] Guid id,
            [FromServices] IRepository<KnowledgeBaseArticle> articleRepo,
            [FromServices] IRepository<KnowledgeEmbedding> embeddingRepo,
            CancellationToken token) =>
        {
            var embeddings = await embeddingRepo.Query()
                .Where(e => e.KnowledgeBaseArticleId == id)
                .Select(e => e.Id.ToString())
                .ToListAsync(token);
            foreach (var eid in embeddings)
            {
                await embeddingRepo.DeleteAsync(eid);
            }

            var ok = await articleRepo.DeleteAsync(id.ToString());
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteKnowledgeBaseArticle")
        .WithSummary("Delete article and embeddings");

        group.MapPost("/{id}/regenerate", async (
            [FromRoute] Guid id,
            [FromServices] IRepository<KnowledgeBaseArticle> repo,
            [FromServices] IKnowledgeBuilderService kbService,
            CancellationToken token) =>
        {
            var article = await repo.GetAsync(id.ToString());
            if (article is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(article.LinkedTicketId))
                return Results.Problem("Article not linked to ticket", statusCode: 400);

            var regenerated = await kbService.GenerateDraftFromResolvedTicketAsync(article.LinkedTicketId, token, true);
            return regenerated is not null ? Results.Ok(regenerated) : Results.Problem("Regeneration failed", statusCode: 500);
        })
        .WithName("RegenerateKnowledgeBaseArticle")
        .WithSummary("Regenerate article from linked ticket");

        group.MapGet("/health", async (
            [FromServices] HelpdeskDbContext db,
            CancellationToken token) =>
        {
            var total = await db.KnowledgeBaseArticles.CountAsync(token);
            var draft = await db.KnowledgeBaseArticles.CountAsync(a => a.State == KnowledgeBaseArticleState.Draft, token);
            var published = await db.KnowledgeBaseArticles.CountAsync(a => a.State == KnowledgeBaseArticleState.Published, token);
            var unresolvedTickets = await db.Tickets
                .Where(t => t.State != TicketState.Resolved)
                .CountAsync(t => !db.KnowledgeBaseArticles.Any(a => a.LinkedTicketId == t.Id), token);
            var orphanedChunks = await db.KnowledgeEmbeddings
                .CountAsync(e => e.KnowledgeBaseArticleId == null ||
                                  !db.KnowledgeBaseArticles.Any(a => a.Id == e.KnowledgeBaseArticleId), token);
            var indexedArticles = await db.KnowledgeBaseArticles
                .CountAsync(a => db.KnowledgeEmbeddings.Any(e => e.KnowledgeBaseArticleId == a.Id), token);
            var indexedChunkCount = await db.KnowledgeEmbeddings
                .CountAsync(e => e.SourceType == "KB", token);
            var emailChunkCount = await db.KnowledgeEmbeddings
                .CountAsync(e => e.SourceType == "Email", token);
            var coverage = total == 0 ? 0 : (double)indexedArticles / total;
            var suggestionFeedbackCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "suggestion", token);
            var helpfulSuggestionCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "suggestion" && x.FeedbackValue == "helpful", token);
            var notHelpfulSuggestionCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "suggestion" && x.FeedbackValue == "not_helpful", token);
            var suggestionHelpfulRate = suggestionFeedbackCount == 0
                ? 0
                : (double)helpfulSuggestionCount / suggestionFeedbackCount;
            var automationFeedbackCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "automation", token);
            var automationResolvedCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "automation" && x.FeedbackValue == "resolved", token);
            var automationNotResolvedCount = await db.TicketAiFeedback
                .CountAsync(x => x.FeedbackType == "automation" && x.FeedbackValue == "not_resolved", token);
            var automationResolvedRate = automationFeedbackCount == 0
                ? 0
                : (double)automationResolvedCount / automationFeedbackCount;
            var aiAuditCount = await db.AiOperationAuditRecords.CountAsync(token);
            var lastAiAuditAt = await db.AiOperationAuditRecords
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTimeOffset?)x.CreatedAt)
                .FirstOrDefaultAsync(token);
            var knowledgeBuildAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "kb-build", token);
            var knowledgeDraftFromTicketAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "kb-draft-from-ticket", token);
            var requesterReplyAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "ticket-requester-reply", token);
            var requesterReplyApprovedAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "ticket-requester-reply-approved", token);
            var automationApprovedAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "ticket-automation-approved", token);
            var runtimeFallbackAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "runtime-chat-fallback", token);
            var runtimeFailureAuditCount = await db.AiOperationAuditRecords
                .CountAsync(x => x.OperationName == "runtime-chat-failure", token);
            var lastRuntimeAuditAt = await db.AiOperationAuditRecords
                .Where(x => x.OperationName == "runtime-chat-fallback" || x.OperationName == "runtime-chat-failure")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTimeOffset?)x.CreatedAt)
                .FirstOrDefaultAsync(token);
            var topRuntimeFallback = await db.AiOperationAuditRecords
                .Where(x => x.OperationName == "runtime-chat-fallback")
                .GroupBy(x => x.ProviderName)
                .Select(g => new { ProviderName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.ProviderName)
                .FirstOrDefaultAsync(token);
            var topRuntimeFailure = await db.AiOperationAuditRecords
                .Where(x => x.OperationName == "runtime-chat-failure")
                .GroupBy(x => x.ProviderName)
                .Select(g => new { ProviderName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.ProviderName)
                .FirstOrDefaultAsync(token);
            var recentRuntimeEvidence = await db.AiOperationAuditRecords
                .Where(x => x.OperationName == "runtime-chat-fallback" || x.OperationName == "runtime-chat-failure")
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => $"{x.CreatedAt.LocalDateTime:g}: {x.OperationName} - {x.ProviderName} - {x.Notes}")
                .ToListAsync(token);

            return Results.Ok(new KnowledgeBaseHealthDto
            {
                TotalArticles = total,
                DraftArticles = draft,
                PublishedArticles = published,
                UnresolvedTicketsWithoutKb = unresolvedTickets,
                IndexedArticles = indexedArticles,
                IndexedChunkCount = indexedChunkCount,
                OrphanedChunkCount = orphanedChunks,
                EmailChunkCount = emailChunkCount,
                IndexedArticleCoverage = coverage,
                SuggestionFeedbackCount = suggestionFeedbackCount,
                HelpfulSuggestionCount = helpfulSuggestionCount,
                NotHelpfulSuggestionCount = notHelpfulSuggestionCount,
                SuggestionHelpfulRate = suggestionHelpfulRate,
                AutomationFeedbackCount = automationFeedbackCount,
                AutomationResolvedCount = automationResolvedCount,
                AutomationNotResolvedCount = automationNotResolvedCount,
                AutomationResolvedRate = automationResolvedRate,
                AiAuditCount = aiAuditCount,
                LastAiAuditAt = lastAiAuditAt,
                KnowledgeBuildAuditCount = knowledgeBuildAuditCount,
                KnowledgeDraftFromTicketAuditCount = knowledgeDraftFromTicketAuditCount,
                RequesterReplyAuditCount = requesterReplyAuditCount,
                RequesterReplyApprovedAuditCount = requesterReplyApprovedAuditCount,
                AutomationApprovedAuditCount = automationApprovedAuditCount,
                RuntimeFallbackAuditCount = runtimeFallbackAuditCount,
                RuntimeFailureAuditCount = runtimeFailureAuditCount,
                LastRuntimeAuditAt = lastRuntimeAuditAt,
                TopRuntimeFallbackProvider = topRuntimeFallback?.ProviderName,
                TopRuntimeFallbackCount = topRuntimeFallback?.Count ?? 0,
                TopRuntimeFailureProvider = topRuntimeFailure?.ProviderName,
                TopRuntimeFailureCount = topRuntimeFailure?.Count ?? 0,
                RecentRuntimeEvidence = recentRuntimeEvidence
            });
        })
        .WithName("GetKnowledgeBaseHealth")
        .WithSummary("Knowledge base health stats");

        group.MapPost("/reindex", async (
            [FromServices] HelpdeskDbContext db,
            [FromServices] IEmbeddingService embeddingService,
            [FromServices] IKnowledgeVectorStore vectorStore,
            CancellationToken token) =>
        {
            var articles = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .Where(a => !string.IsNullOrWhiteSpace(a.OrganizationId))
                .ToListAsync(token);

            var result = new KnowledgeBaseReindexResultDto
            {
                RequestedArticles = articles.Count
            };

            foreach (var article in articles)
            {
                var content = article.Summary ?? article.Resolution ?? article.Problem;
                if (string.IsNullOrWhiteSpace(content))
                {
                    result.SkippedArticles++;
                    continue;
                }

                var chunks = TextChunker.Split(content).ToList();
                if (chunks.Count == 0)
                {
                    result.SkippedArticles++;
                    continue;
                }

                var vectors = await embeddingService.CreateEmbeddingsAsync(
                    article.OrganizationId,
                    chunks.Select(chunk => chunk.Text),
                    token);

                var now = DateTime.UtcNow;
                var records = vectors
                    .Select((vector, index) => new KnowledgeVectorRecord(
                        article.Id,
                        article.OrganizationId,
                        "KB",
                        article.Id.ToString(),
                        article.Title,
                        index,
                        index < chunks.Count ? chunks[index].ChunkId : index.ToString(),
                        index < chunks.Count ? chunks[index].Text : content,
                        new Dictionary<string, string>
                        {
                            ["articleId"] = article.Id.ToString(),
                            ["ticketId"] = article.LinkedTicketId ?? string.Empty,
                            ["state"] = article.State.ToString()
                        },
                        vector,
                        now))
                    .ToList();

                await vectorStore.ReplaceArticleChunksAsync(
                    article.Id,
                    article.OrganizationId,
                    "KB",
                    article.Id.ToString(),
                    records,
                    token);

                result.ReindexedArticles++;
            }

            await db.SaveChangesAsync(token);
            return Results.Ok(result);
        })
        .WithName("ReindexKnowledgeBase")
        .WithSummary("Rebuild KB article chunk/vector records");
    }

    private static KnowledgeArticleAutomationTargetDto? MapAutomationTarget(KnowledgeBaseArticle article, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(article.AutomationBindingId)
            && string.IsNullOrWhiteSpace(article.AutomationRequestFormId)
            && article.AutomationTaskTemplateId is null)
        {
            return null;
        }

        return new KnowledgeArticleAutomationTargetDto
        {
            BindingId = article.AutomationBindingId,
            RequestFormId = article.AutomationRequestFormId,
            TaskTemplateId = article.AutomationTaskTemplateId,
            TaskTemplateName = article.AutomationTaskTemplateName,
            OrchestrationRequestDefinitionId = article.AutomationOrchestrationRequestDefinitionId,
            OrchestrationJobDefinitionId = article.AutomationOrchestrationJobDefinitionId,
            Enabled = enabled
        };
    }

    private static void ClearAutomationTarget(KnowledgeBaseArticle article)
    {
        article.AutomationBindingId = null;
        article.AutomationRequestFormId = null;
        article.AutomationTaskTemplateId = null;
        article.AutomationTaskTemplateName = null;
        article.AutomationOrchestrationRequestDefinitionId = null;
        article.AutomationOrchestrationJobDefinitionId = null;
    }
}
