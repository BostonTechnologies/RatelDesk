using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.KB;

public interface IKnowledgeBuilderService
{
    Task<KnowledgeBaseArticle> BuildArticleAsync(string prompt, CancellationToken token);
    Task<KnowledgeBaseArticle?> GenerateDraftFromResolvedTicketAsync(string ticketId, CancellationToken token, bool regenerate = false);
}

