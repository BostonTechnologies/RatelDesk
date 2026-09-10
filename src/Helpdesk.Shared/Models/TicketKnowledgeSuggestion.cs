namespace Helpdesk.Shared.Models;

public class TicketKnowledgeSuggestion
{
    public Guid Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public Guid KnowledgeBaseArticleId { get; set; }
    public KnowledgeBaseArticle Article { get; set; } = null!;
    public double Score { get; set; }
    public DateTime CreatedAt { get; set; }
}
