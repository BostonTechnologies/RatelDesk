using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class SupportNotificationRecipientDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SupportGroupId { get; set; }
    public string? SupportGroupName { get; set; }
}

public class SupportNotificationRecipientPreviewDto
{
    public string CustomerOrganizationId { get; set; } = string.Empty;
    public SupportNotificationEventType EventType { get; set; }
    public SupportNotificationChannel Channel { get; set; }
    public List<SupportNotificationRecipientDto> Recipients { get; set; } = new();
}
