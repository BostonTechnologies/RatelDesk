using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SupportNotificationSubscription
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string CustomerOrganizationId { get; set; } = string.Empty;
    public SupportNotificationEventType EventType { get; set; } = SupportNotificationEventType.TicketCreatedUnassigned;
    public SupportNotificationRecipientType RecipientType { get; set; } = SupportNotificationRecipientType.SupportGroup;
    public string RecipientId { get; set; } = string.Empty;
    public SupportNotificationChannel Channel { get; set; } = SupportNotificationChannel.Email;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
