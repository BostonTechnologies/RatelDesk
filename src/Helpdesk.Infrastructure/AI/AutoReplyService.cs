using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.AI;

public class AutoReplyService(
    IAiClient aiClient,
    HelpdeskDbContext db,
    ILogger<AutoReplyService> logger) : IAutoReplyService
{
    private readonly IAiClient _aiClient = aiClient;
    private readonly HelpdeskDbContext _db = db;
    private readonly ILogger<AutoReplyService> _logger = logger;

    public async Task<string> PrepareReplyAsync(Ticket ticket, KnowledgeBaseArticle article, CancellationToken token)
    {
        var provider = await _db.AiProviders.FirstAsync(p => p.IsEnabled, token);
        var articleText = $"{article.Summary} {article.Resolution}";
        var prompt = $"Use the following article to draft a reply: {articleText}";
        var reply = await _aiClient.ChatAsync(provider, prompt, provider.DefaultModel, token);
        _logger.LogInformation("Auto reply generated for ticket {TicketId}", ticket.Id);
        return reply;
    }
}

