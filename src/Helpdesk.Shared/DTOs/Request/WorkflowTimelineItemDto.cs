namespace Helpdesk.Shared.DTOs.Request;

public sealed class WorkflowTimelineItemDto
{
    public Guid NotificationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string? CorrelationId { get; set; }
    public string? RequestId { get; set; }
    public string? TaskId { get; set; }
    public string? TrackingId { get; set; }
    public string? ExecutionId { get; set; }
    public string? PayloadJson { get; set; }
}
