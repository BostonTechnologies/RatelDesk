namespace Helpdesk.Shared.Models;

public class TicketAiFeedback
{
    public Guid Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = string.Empty;
    public string FeedbackValue { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? RequestId { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
