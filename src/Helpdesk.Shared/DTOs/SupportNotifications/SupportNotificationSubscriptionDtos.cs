using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class SupportNotificationSubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerOrganizationId { get; set; } = string.Empty;
    public SupportNotificationEventType EventType { get; set; }
    public SupportNotificationRecipientType RecipientType { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public SupportNotificationChannel Channel { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class CreateSupportNotificationSubscriptionDto
{
    [Required]
    public string CustomerOrganizationId { get; set; } = string.Empty;

    public SupportNotificationEventType EventType { get; set; } = SupportNotificationEventType.TicketCreatedUnassigned;
    public SupportNotificationRecipientType RecipientType { get; set; } = SupportNotificationRecipientType.SupportGroup;

    [Required]
    public string RecipientId { get; set; } = string.Empty;

    public SupportNotificationChannel Channel { get; set; } = SupportNotificationChannel.Email;
    public bool IsEnabled { get; set; } = true;
}

public class UpdateSupportNotificationSubscriptionDto
{
    [Required]
    public string CustomerOrganizationId { get; set; } = string.Empty;

    public SupportNotificationEventType EventType { get; set; } = SupportNotificationEventType.TicketCreatedUnassigned;
    public SupportNotificationRecipientType RecipientType { get; set; } = SupportNotificationRecipientType.SupportGroup;

    [Required]
    public string RecipientId { get; set; } = string.Empty;

    public SupportNotificationChannel Channel { get; set; } = SupportNotificationChannel.Email;
    public bool IsEnabled { get; set; } = true;
}
