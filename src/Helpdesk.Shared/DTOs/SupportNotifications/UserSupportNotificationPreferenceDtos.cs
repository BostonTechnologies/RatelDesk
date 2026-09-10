using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class UserSupportNotificationPreferenceDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public SupportNotificationEventType EventType { get; set; }
    public SupportNotificationChannel Channel { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class UpsertUserSupportNotificationPreferenceDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public SupportNotificationEventType EventType { get; set; }
    public SupportNotificationChannel Channel { get; set; } = SupportNotificationChannel.Email;
    public bool IsEnabled { get; set; } = true;
}
