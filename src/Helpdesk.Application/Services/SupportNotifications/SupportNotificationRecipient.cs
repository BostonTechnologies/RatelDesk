namespace Helpdesk.Application.Services.SupportNotifications;

public sealed record SupportNotificationRecipient(
    string UserId,
    string UserName,
    string Email,
    string? SupportGroupId = null,
    string? SupportGroupName = null);
