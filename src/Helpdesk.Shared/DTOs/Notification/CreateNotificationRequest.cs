namespace Helpdesk.Shared.DTOs.Notification;

public class CreateNotificationRequest
{
    public string? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string? Source { get; set; }
    public string? Category { get; set; }
    public string? TenantId { get; set; }
    public string? Reference { get; set; }
    public string? CorrelationId { get; set; }
    public string? Link { get; set; }
}
