using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Application.Services.KB;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.AI;

public class TicketUnderstandingService(
    HelpdeskDbContext db,
    IKnowledgeRetrievalService retrievalService,
    ILogger<TicketUnderstandingService> logger) : ITicketUnderstandingService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IKnowledgeRetrievalService _retrievalService = retrievalService;
    private readonly ILogger<TicketUnderstandingService> _logger = logger;

    public Task<string> UnderstandAsync(Ticket ticket, CancellationToken token) => Task.FromResult(string.Empty);

    public async Task<TicketKnowledgeSuggestion?> AnalyzeTicketAsync(Ticket ticket, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ticket.OrganizationId))
            return null;

        var result = (await _retrievalService.RetrieveAsync(ticket.OrganizationId, ticket.Description, 1, token)).FirstOrDefault();
        if (result is null)
            return null;

        var suggestion = new TicketKnowledgeSuggestion
        {
            TicketId = ticket.Id,
            KnowledgeBaseArticleId = result.Article.Id,
            Article = result.Article,
            Score = result.Score
        };
        _db.TicketKnowledgeSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(token);
        _logger.LogInformation("Knowledge suggestion {SuggestionId} created for ticket {TicketId}", suggestion.Id, ticket.Id);
        return suggestion;
    }
}
