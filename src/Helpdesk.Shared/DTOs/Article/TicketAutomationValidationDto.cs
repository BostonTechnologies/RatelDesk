namespace Helpdesk.Shared.DTOs.Article;

public sealed class TicketAutomationValidationDto
{
    public string ArticleId { get; set; } = string.Empty;
    public bool CanApprove { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
