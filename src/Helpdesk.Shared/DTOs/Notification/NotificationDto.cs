namespace Helpdesk.Shared.DTOs.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReadUtc { get; set; }
    public bool IsRead { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public string? TenantId { get; set; }
    public string? Reference { get; set; }
    public string? CorrelationId { get; set; }
    public string? Link { get; set; }
    public bool IsGlobal { get; set; }
}
