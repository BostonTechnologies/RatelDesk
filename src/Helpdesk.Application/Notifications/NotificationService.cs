using Helpdesk.Shared.DTOs.Notification;
using Helpdesk.Shared.DTOs;
using Helpdesk.Application.Observability;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Notifications;

public class NotificationService(
    INotificationRepository repository,
    INotificationEventBus? eventBus = null,
    ILogger<NotificationService>? logger = null) : INotificationService
{
    private readonly INotificationRepository _repository = repository;
    private readonly INotificationEventBus? _eventBus = eventBus;
    private readonly ILogger<NotificationService>? _logger = logger;

    public async Task CreateNotificationAsync(CreateNotificationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.", nameof(request));

        using var activity = HelpdeskTelemetry.StartActivity("helpdesk.notification.create");
        activity?.SetTag("notification.severity", request.Severity.ToString());
        activity?.SetTag("notification.category", request.Category);
        activity?.SetTag("notification.source", request.Source);
        activity?.SetTag("notification.tenant_id", request.TenantId);
        activity?.SetTag("notification.reference", request.Reference);
        activity?.SetTag("notification.correlation_id", request.CorrelationId);
        activity?.SetTag("notification.is_global", string.IsNullOrWhiteSpace(request.UserId));

        var created = await _repository.CreateNotificationAsync(request, ct);
        HelpdeskTelemetry.RecordNotificationCreated(request);
        _logger?.LogInformation(
            "Helpdesk notification created. Severity={Severity} Category={Category} Source={Source} TenantId={TenantId} Reference={Reference} CorrelationId={CorrelationId} IsGlobal={IsGlobal}",
            request.Severity,
            request.Category,
            request.Source,
            request.TenantId,
            request.Reference,
            request.CorrelationId,
            string.IsNullOrWhiteSpace(request.UserId));

        _eventBus?.Publish(created);
    }

    public Task<NotificationDto?> GetNotificationByIdAsync(Guid notificationId, string userId, CancellationToken ct)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("Notification ID is required.", nameof(notificationId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        return _repository.GetNotificationByIdAsync(notificationId, userId, ct);
    }

    public async Task<List<NotificationDto>> GetNotificationsForUserAsync(string userId, int page, int pageSize, CancellationToken ct)
    {
        return await GetNotificationsForUserAsync(userId, page, pageSize, null, null, null, null, null, null, null, null, ct);
    }

    public async Task<List<NotificationDto>> GetNotificationsForUserAsync(
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
        CancellationToken ct)
    {
        var result = await GetNotificationsPageForUserAsync(
            userId,
            page,
            pageSize,
            searchTerm,
            severity,
            source,
            category,
            from,
            to,
            sortBy,
            sortDir,
            ct);

        return result.Items;
    }

    public async Task<PagedResponse<NotificationDto>> GetNotificationsPageForUserAsync(
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
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);
        var skip = (safePage - 1) * safePageSize;

        var items = await _repository.GetNotificationsForUserAsync(
            userId,
            skip,
            safePageSize,
            searchTerm,
            severity,
            source,
            category,
            from,
            to,
            sortBy,
            sortDir,
            ct);

        var total = await _repository.CountNotificationsForUserAsync(
            userId,
            searchTerm,
            severity,
            source,
            category,
            from,
            to,
            ct);

        return new PagedResponse<NotificationDto>
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = total,
            Items = items.ToList()
        };
    }

    public async Task<List<NotificationDto>> GetUnreadNotificationsAsync(string userId, int page, int pageSize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID required", nameof(userId));

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);
        var skip = (safePage - 1) * safePageSize;

        return (await _repository.GetUnreadNotificationsAsync(userId, skip, safePageSize, ct)).ToList();
    }

    public async Task<List<NotificationDto>> GetUnreadErrorNotificationsAsync(string userId, int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID required", nameof(userId));

        var safeTake = take < 1 ? 20 : Math.Min(take, 100);
        return (await _repository.GetUnreadErrorNotificationsAsync(userId, safeTake, ct)).ToList();
    }

    public async Task<List<NotificationDto>> GetDomainEventTimelineAsync(
        string userId,
        string? reference,
        string? correlationId,
        string? tenantId,
        int take,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(reference) && string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Either reference or correlation ID is required.");

        var safeTake = take < 1 ? 200 : Math.Min(take, 1000);

        var items = await _repository.GetDomainEventTimelineAsync(
            userId,
            string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
            safeTake,
            ct);

        return items.ToList();
    }

    public Task<NotificationSummaryDto> GetNotificationSummaryAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        return _repository.GetNotificationSummaryAsync(userId, ct);
    }

    public async Task<NotificationSummaryDto> GetErrorNotificationSummaryAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        var unreadErrorCount = await _repository.CountUnreadErrorNotificationsAsync(userId, ct);
        return new NotificationSummaryDto { UnreadErrorCount = unreadErrorCount };
    }

    public async Task MarkNotificationReadAsync(Guid notificationId, string userId, CancellationToken ct)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("Notification ID is required.", nameof(notificationId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        var marked = await _repository.MarkNotificationReadAsync(notificationId, userId, DateTime.UtcNow, ct);
        if (!marked)
            throw new InvalidOperationException("Notification could not be marked as read for this user.");
    }

    public async Task<int> MarkNotificationsReadAsync(IReadOnlyCollection<Guid> notificationIds, string userId, CancellationToken ct)
    {
        if (notificationIds is null || notificationIds.Count == 0)
            return 0;

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        var validIds = notificationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (validIds.Length == 0)
            return 0;

        return await _repository.MarkNotificationsReadAsync(validIds, userId, DateTime.UtcNow, ct);
    }

    public Task NotifyUserAsync(string userId, string message, string ticketId)
    {
        var request = new CreateNotificationRequest
        {
            UserId = userId,
            Title = "Ticket Updated",
            Message = message,
            Severity = NotificationSeverity.Info,
            Category = "Workflow",
            Source = "WorkLog",
            Link = string.IsNullOrWhiteSpace(ticketId) ? null : $"/incidents/{ticketId}"
        };

        return CreateNotificationAsync(request, CancellationToken.None);
    }
}
