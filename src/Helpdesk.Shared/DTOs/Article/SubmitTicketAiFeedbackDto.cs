namespace Helpdesk.Shared.DTOs.Article;

public sealed class SubmitTicketAiFeedbackDto
{
    public string FeedbackType { get; set; } = string.Empty;
    public string FeedbackValue { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? RequestId { get; set; }
    public string? Notes { get; set; }
}
