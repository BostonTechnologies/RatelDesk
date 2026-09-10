namespace Helpdesk.Shared.DTOs.Notification;

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public int UnreadErrorCount { get; set; }
    public int TotalCount { get; set; }
}
