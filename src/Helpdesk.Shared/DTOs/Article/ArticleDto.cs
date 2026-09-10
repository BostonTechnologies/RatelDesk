using System;
using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Article;

public class ArticleDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public bool IsPublic { get; set; }

    public KnowledgeBaseArticleState State { get; set; }
    public string? LinkedTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
    public KnowledgeArticleAutomationTargetDto? AutomationTarget { get; set; }
}
