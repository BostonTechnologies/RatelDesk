namespace Helpdesk.Shared.DTOs.Article;

public sealed class TicketAutomationRunDto
{
    public string RequestId { get; set; } = string.Empty;
    public string RequestTrackingId { get; set; } = string.Empty;
    public string? RequestState { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? RequestTaskId { get; set; }
    public string? RequestTaskTitle { get; set; }
    public string? RequestTaskStatus { get; set; }
    public string? LastAutomationStatus { get; set; }
    public string? OrchestrationRunId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ArticleId { get; set; }
    public string? ArticleTitle { get; set; }
    public string? AutomationBindingId { get; set; }
}
