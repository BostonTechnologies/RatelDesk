using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class UserSupportNotificationPreference
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string UserId { get; set; } = string.Empty;
    public SupportNotificationEventType EventType { get; set; } = SupportNotificationEventType.TicketCreatedUnassigned;
    public SupportNotificationChannel Channel { get; set; } = SupportNotificationChannel.Email;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
