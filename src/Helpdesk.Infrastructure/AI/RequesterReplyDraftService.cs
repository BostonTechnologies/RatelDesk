using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.AI;

public sealed class RequesterReplyDraftService(
    IAiRuntime aiRuntime,
    IKnowledgeRetrievalService retrievalService,
    IAiPromptTemplateService promptTemplateService,
    IAiOperationAuditService auditService,
    HelpdeskDbContext db,
    ILogger<RequesterReplyDraftService> logger) : IRequesterReplyDraftService
{
    private readonly IAiRuntime _aiRuntime = aiRuntime;
    private readonly IKnowledgeRetrievalService _retrievalService = retrievalService;
    private readonly IAiPromptTemplateService _promptTemplateService = promptTemplateService;
    private readonly IAiOperationAuditService _auditService = auditService;
    private readonly HelpdeskDbContext _db = db;
    private readonly ILogger<RequesterReplyDraftService> _logger = logger;

    public async Task<RequesterReplyDraftDto?> GenerateAsync(Ticket ticket, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ticket.OrganizationId))
        {
            return null;
        }

        var settings = await _db.OrganizationAiKbSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == ticket.OrganizationId, token);
        if (settings is null || (!settings.EnableAiAnswers && !settings.EnableAiSearch))
        {
            return null;
        }

        var retrieval = (await _retrievalService.RetrieveAsync(ticket.OrganizationId, ticket.Description, 1, token))
            .FirstOrDefault();
        if (retrieval is null)
        {
            return null;
        }

        var followUpQuestions = BuildFollowUpQuestions(retrieval);
        var suggestedLink = $"/kb/{retrieval.Article.Id}";

        if (!retrieval.IsHighConfidence)
        {
            return new RequesterReplyDraftDto
            {
                RequiresClarification = true,
                ConfidenceLabel = retrieval.ConfidenceLabel,
                SuggestedArticleTitle = retrieval.Article.Title,
                SuggestedArticleLink = suggestedLink,
                DraftReply = "Please confirm a few details so we can validate the best next step for your issue.",
                FollowUpQuestions = followUpQuestions
            };
        }

        var runtime = await _aiRuntime.CreateChatClientAsync(
            new AiChatRuntimeRequest(ticket.OrganizationId, Scenario: "ticket-requester-reply", SubjectId: ticket.Id),
            token);
        var systemPrompt = _promptTemplateService.Render(
            "Ticket.RequesterReplyDraft.System",
            new Dictionary<string, string?>());
        var evidence = string.Join(
            Environment.NewLine,
            retrieval.Evidence.Select(x => $"- {x.Text}"));
        var userPrompt = $"""
Ticket title: {ticket.Title}
Ticket description: {ticket.Description}
Suggested article: {retrieval.Article.Title}
Article summary: {retrieval.Article.Summary}
Article resolution: {retrieval.Article.Resolution}
Grounding evidence:
{evidence}
Write a concise helpdesk response to the requester. Use plain language, do not mention AI, and keep it actionable.
""";
        var response = await runtime.Client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            ],
            cancellationToken: token);

        await _auditService.RecordAsync(
            new AiOperationAuditEntry(
                "ticket-requester-reply",
                ticket.OrganizationId,
                runtime.ProviderName,
                runtime.ModelId,
                SubjectId: ticket.Id),
            token);

        _logger.LogInformation("Requester reply draft generated for ticket {TicketId}", ticket.Id);

        return new RequesterReplyDraftDto
        {
            RequiresClarification = false,
            ConfidenceLabel = retrieval.ConfidenceLabel,
            SuggestedArticleTitle = retrieval.Article.Title,
            SuggestedArticleLink = suggestedLink,
            DraftReply = response.Text ?? string.Empty,
            FollowUpQuestions = followUpQuestions
        };
    }

    private static List<string> BuildFollowUpQuestions(KnowledgeRetrievalResult retrieval)
    {
        return
        [
            "Which service or application is affected?",
            "What exact error message or behavior are you seeing?",
            "When did the issue start, and is it still happening now?"
        ];
    }
}
