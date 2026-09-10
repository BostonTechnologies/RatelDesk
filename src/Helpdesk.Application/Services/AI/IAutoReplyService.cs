using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface IAutoReplyService
{
    Task<string> PrepareReplyAsync(Ticket ticket, KnowledgeBaseArticle article, CancellationToken token);
}

