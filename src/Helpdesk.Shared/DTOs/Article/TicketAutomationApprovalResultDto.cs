namespace Helpdesk.Shared.DTOs.Article;

public sealed class TicketAutomationApprovalResultDto
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestTrackingId { get; set; } = string.Empty;
    public string? RequestTaskId { get; set; }
    public string? RequestTaskStatus { get; set; }
    public string AutomationBindingId { get; set; } = string.Empty;
    public string ArticleId { get; set; } = string.Empty;
}
