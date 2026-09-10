namespace Helpdesk.Application.Notifications;

using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Notification;

public interface INotificationService
{
    Task CreateNotificationAsync(CreateNotificationRequest request, CancellationToken ct);
    Task<NotificationDto?> GetNotificationByIdAsync(Guid notificationId, string userId, CancellationToken ct);
    Task<List<NotificationDto>> GetNotificationsForUserAsync(string userId, int page, int pageSize, CancellationToken ct);
    Task<List<NotificationDto>> GetNotificationsForUserAsync(
        string userId,
        int page,
        int pageSize,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sortBy,
        string? sortDir,
        CancellationToken ct);
    Task<PagedResponse<NotificationDto>> GetNotificationsPageForUserAsync(
        string userId,
        int page,
        int pageSize,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sortBy,
        string? sortDir,
        CancellationToken ct);
    Task<List<NotificationDto>> GetUnreadNotificationsAsync(string userId, int page, int pageSize, CancellationToken ct);
    Task<List<NotificationDto>> GetUnreadErrorNotificationsAsync(string userId, int take, CancellationToken ct);
    Task<List<NotificationDto>> GetDomainEventTimelineAsync(
        string userId,
        string? reference,
        string? correlationId,
        string? tenantId,
        int take,
        CancellationToken ct);
    Task<NotificationSummaryDto> GetNotificationSummaryAsync(string userId, CancellationToken ct);
    Task<NotificationSummaryDto> GetErrorNotificationSummaryAsync(string userId, CancellationToken ct);
    Task MarkNotificationReadAsync(Guid notificationId, string userId, CancellationToken ct);
    Task<int> MarkNotificationsReadAsync(IReadOnlyCollection<Guid> notificationIds, string userId, CancellationToken ct);

    // Compatibility method used by existing workflows that emit a ticket update notification.
    Task NotifyUserAsync(string userId, string message, string ticketId);
}
