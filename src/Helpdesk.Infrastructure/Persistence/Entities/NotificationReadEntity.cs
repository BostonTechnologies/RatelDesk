namespace Helpdesk.Infrastructure.Persistence.Entities;

public class NotificationReadEntity
{
    public Guid NotificationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ReadUtc { get; set; }

    public NotificationEntity Notification { get; set; } = default!;
}
