using Helpdesk.Shared.DTOs.Notification;

namespace Helpdesk.Application.Notifications;

public interface INotificationRepository
{
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request, CancellationToken ct);
    Task<NotificationDto?> GetNotificationByIdAsync(Guid id, string userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> GetNotificationsForUserAsync(string userId, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> GetNotificationsForUserAsync(
        string userId,
        int skip,
        int take,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sortBy,
        string? sortDir,
        CancellationToken ct);
    Task<int> CountNotificationsForUserAsync(
        string userId,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> GetUnreadNotificationsAsync(string userId, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> GetUnreadErrorNotificationsAsync(string userId, int take, CancellationToken ct);
    Task<int> CountUnreadErrorNotificationsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> GetDomainEventTimelineAsync(
        string userId,
        string? reference,
        string? correlationId,
        string? tenantId,
        int take,
        CancellationToken ct);
    Task<NotificationSummaryDto> GetNotificationSummaryAsync(string userId, CancellationToken ct);
    Task<bool> MarkNotificationReadAsync(Guid notificationId, string userId, DateTime readUtc, CancellationToken ct);
    Task<int> MarkNotificationsReadAsync(IReadOnlyCollection<Guid> notificationIds, string userId, DateTime readUtc, CancellationToken ct);
}
