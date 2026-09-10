namespace Helpdesk.Shared.DTOs.Notification;

public class BulkMarkNotificationReadRequest
{
    public List<Guid> Ids { get; set; } = new();
}
