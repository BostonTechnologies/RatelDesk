using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Logging;
using Helpdesk.Application.Services.AI;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Application.Services.KB;

public class KnowledgeSuggestionService(
    HelpdeskDbContext db,
    IKnowledgeRetrievalService retrievalService,
    ILogger<KnowledgeSuggestionService> logger) : IKnowledgeSuggestionService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IKnowledgeRetrievalService _retrievalService = retrievalService;
    private readonly ILogger<KnowledgeSuggestionService> _logger = logger;

    public async Task<IEnumerable<TicketKnowledgeSuggestion>> SuggestAsync(Ticket ticket, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ticket.OrganizationId))
            return Enumerable.Empty<TicketKnowledgeSuggestion>();

        var results = await _retrievalService.RetrieveAsync(ticket.OrganizationId, ticket.Description, 5, token);
        var suggestions = results
            .Select(result => new TicketKnowledgeSuggestion
            {
                TicketId = ticket.Id,
                KnowledgeBaseArticleId = result.Article.Id,
                Article = result.Article,
                Score = result.Score
            })
            .ToList();
        if (suggestions.Count == 0)
            return suggestions;
        _db.TicketKnowledgeSuggestions.AddRange(suggestions);
        await _db.SaveChangesAsync(token);
        _logger.LogInformation("{Count} suggestions stored for ticket {TicketId}", suggestions.Count, ticket.Id);
        return suggestions;
    }
}
