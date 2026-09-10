using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Helpdesk.Application.Services.AI;
using System.Linq;

namespace Helpdesk.Application.Services.KB;

public class KnowledgeBuilderService(
    IAiRuntime aiRuntime,
    HelpdeskDbContext db,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore,
    IAiPromptTemplateService promptTemplates,
    IAiOperationAuditService aiAudit,
    ILogger<KnowledgeBuilderService> logger) : IKnowledgeBuilderService
{
    private readonly IAiRuntime _aiRuntime = aiRuntime;
    private readonly HelpdeskDbContext _db = db;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IKnowledgeVectorStore _vectorStore = vectorStore;
    private readonly IAiPromptTemplateService _promptTemplates = promptTemplates;
    private readonly IAiOperationAuditService _aiAudit = aiAudit;
    private readonly ILogger<KnowledgeBuilderService> _logger = logger;

    public async Task<KnowledgeBaseArticle> BuildArticleAsync(string prompt, CancellationToken token)
    {
        var runtime = await _aiRuntime.CreateChatClientAsync(
            new AiChatRuntimeRequest(OrganizationId: null, Scenario: "kb-build"),
            token);
        var response = await runtime.Client.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, prompt)],
            cancellationToken: token);
        var content = response.Text ?? string.Empty;
        var article = new KnowledgeBaseArticle
        {
            Title = content.Split('\n').FirstOrDefault() ?? "Draft Article",
            Summary = content,
            Resolution = content
        };
        _db.KnowledgeBaseArticles.Add(article);
        await _db.SaveChangesAsync(token);
        await _aiAudit.RecordAsync(
            new AiOperationAuditEntry(
                "kb-build",
                article.OrganizationId,
                runtime.ProviderName,
                runtime.ModelId,
                SubjectId: article.Id.ToString()),
            token);
        if (!string.IsNullOrWhiteSpace(article.OrganizationId))
            await _embeddingService.CreateEmbeddingAsync(article.OrganizationId, content, token);
        _logger.LogInformation("Draft knowledge base article {ArticleId} created", article.Id);
        return article;
    }

    public async Task<KnowledgeBaseArticle?> GenerateDraftFromResolvedTicketAsync(
        string ticketId,
        CancellationToken token,
        bool regenerate = false)
    {
        var ticket = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId, token);

        if (ticket is null)
        {
            return null;
        }

        if (ticket.State != TicketState.Resolved)
        {
            return null;
        }

        var existing = await _db.KnowledgeBaseArticles
            .Include(a => a.Embeddings)
            .FirstOrDefaultAsync(a =>
                a.LinkedTicketId == ticket.Id &&
                a.OrganizationId == ticket.OrganizationId,
                token);

        if (existing is not null && !regenerate)
        {
            return existing;
        }

        var runtime = await _aiRuntime.CreateChatClientAsync(
            new AiChatRuntimeRequest(ticket.OrganizationId, Scenario: "kb-draft-from-ticket", SubjectId: ticket.Id),
            token);

        if (runtime is null)
        {
            return null;
        }

        var workLogs = await _db.WorkLogs
            .AsNoTracking()
            .Where(w => w.TicketId == ticket.Id)
            .ToListAsync(token);

        var workLogText = workLogs.Count == 0
            ? "No work logs."
            : string.Join("\n", workLogs.Select(w => $"- {w.LoggedAt:u}: {w.NotesText ?? string.Empty} ({w.Hours}h)"));

        var systemPrompt = _promptTemplates.Render(
            "KnowledgeBase.GenerateDraftFromResolvedTicket.System",
            new Dictionary<string, string?>());
        var userPrompt = $"""
Title: {ticket.Title}
Description: {ticket.Description}
Work Logs:
{workLogText}
""";
        var aiResult = await runtime.Client.GetResponseAsync(
            [
                new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.System, systemPrompt),
                new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userPrompt)
            ],
            cancellationToken: token);
        var generatedContent = aiResult.Text ?? string.Empty;
        var lines = generatedContent.Replace("\r", string.Empty).Split('\n');
        var parsedTitle = lines.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(parsedTitle))
        {
            parsedTitle = "Draft Article";
        }

        var parsedContent = string.Join('\n', lines.Skip(1)).Trim();
        if (string.IsNullOrWhiteSpace(parsedContent))
        {
            parsedContent = generatedContent;
        }

        var now = DateTime.UtcNow;
        var article = existing ?? new KnowledgeBaseArticle
        {
            OrganizationId = ticket.OrganizationId ?? string.Empty,
            LinkedTicketId = ticket.Id,
            SourceIncidentId = ticket.Id,
            Service = "General",
            State = KnowledgeBaseArticleState.Draft,
            CreatedAt = now
        };

        article.Title = parsedTitle;
        article.Summary = parsedContent;
        article.Resolution = parsedContent;
        article.State = KnowledgeBaseArticleState.Draft;
        article.CreatedByModel = runtime.ModelId;
        article.LastRegeneratedAt = now;
        article.UpdatedAt = now;
        if (existing is null)
        {
            _db.KnowledgeBaseArticles.Add(article);
        }

        if (!string.IsNullOrWhiteSpace(ticket.OrganizationId))
        {
            var contentChunks = TextChunker.Split(parsedContent).ToList();
            var embeddings = await _embeddingService.CreateEmbeddingsAsync(
                ticket.OrganizationId,
                contentChunks.Select(chunk => chunk.Text),
                token);

            var chunks = embeddings
                .Select((emb, index) => new KnowledgeVectorRecord(
                    article.Id,
                    ticket.OrganizationId,
                    "KB",
                    article.Id.ToString(),
                    article.Title,
                    index,
                    index < contentChunks.Count ? contentChunks[index].ChunkId : index.ToString(),
                    index < contentChunks.Count ? contentChunks[index].Text : parsedContent,
                    new Dictionary<string, string>
                    {
                        ["articleId"] = article.Id.ToString(),
                        ["ticketId"] = ticket.Id,
                        ["state"] = article.State.ToString()
                    },
                    emb,
                    now))
                .ToList();

            await _vectorStore.ReplaceArticleChunksAsync(
                article.Id,
                ticket.OrganizationId,
                "KB",
                article.Id.ToString(),
                chunks,
                token);
        }

        await _aiAudit.RecordAsync(
            new AiOperationAuditEntry(
                "kb-draft-from-ticket",
                ticket.OrganizationId,
                runtime.ProviderName,
                runtime.ModelId,
                SubjectId: ticket.Id),
            token);

        await _db.SaveChangesAsync(token);
        _logger.LogInformation("Draft knowledge base article {ArticleId} created from ticket {TicketId}", article.Id, ticket.Id);
        return article;
    }
}
