using Helpdesk.Application.Notifications;
using Helpdesk.Infrastructure.Persistence.Entities;
using Helpdesk.Shared.DTOs.Notification;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

public class NotificationRepository(HelpdeskDbContext context) : INotificationRepository
{
    private readonly HelpdeskDbContext _context = context;

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request, CancellationToken ct)
    {
        var isGlobal = string.IsNullOrWhiteSpace(request.UserId);

        var entity = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = isGlobal ? null : request.UserId,
            Title = request.Title,
            Message = request.Message,
            Severity = request.Severity,
            CreatedUtc = DateTime.UtcNow,
            Source = request.Source,
            Category = request.Category,
            TenantId = request.TenantId,
            Reference = request.Reference,
            CorrelationId = request.CorrelationId,
            Link = request.Link,
            IsGlobal = isGlobal
        };

        _context.Notifications.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new NotificationDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Title = entity.Title,
            Message = entity.Message,
            Severity = entity.Severity,
            CreatedUtc = entity.CreatedUtc,
            ReadUtc = entity.ReadUtc,
            IsRead = entity.ReadUtc.HasValue,
            Source = entity.Source,
            Category = entity.Category,
            TenantId = entity.TenantId,
            Reference = entity.Reference,
            CorrelationId = entity.CorrelationId,
            Link = entity.Link,
            IsGlobal = entity.IsGlobal
        };
    }

    public async Task<NotificationDto?> GetNotificationByIdAsync(Guid id, string userId, CancellationToken ct)
    {
        var readsForUser = _context.NotificationReads.AsNoTracking().Where(x => x.UserId == userId);

        var query =
            from n in _context.Notifications.AsNoTracking()
            where n.Id == id && (n.IsGlobal || n.UserId == userId)
            join r in readsForUser on n.Id equals r.NotificationId into readGroup
            from r in readGroup.DefaultIfEmpty()
            select new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                CreatedUtc = n.CreatedUtc,
                ReadUtc = n.IsGlobal ? (r != null ? r.ReadUtc : null) : n.ReadUtc,
                IsRead = n.IsGlobal ? r != null : n.ReadUtc.HasValue,
                Source = n.Source,
                Category = n.Category,
                TenantId = n.TenantId,
                Reference = n.Reference,
                CorrelationId = n.CorrelationId,
                Link = n.Link,
                IsGlobal = n.IsGlobal
            };

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsForUserAsync(string userId, int skip, int take, CancellationToken ct)
    {
        return await GetNotificationsForUserAsync(userId, skip, take, null, null, null, null, null, null, null, null, ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsForUserAsync(
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
        CancellationToken ct)
    {
        var readsForUser = _context.NotificationReads.AsNoTracking().Where(x => x.UserId == userId);
        var notificationsQuery = ApplySorting(
            BuildNotificationsQuery(userId, searchTerm, severity, source, category, from, to),
            sortBy,
            sortDir);

        var query =
            from n in notificationsQuery
            join r in readsForUser on n.Id equals r.NotificationId into readGroup
            from r in readGroup.DefaultIfEmpty()
            select new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                CreatedUtc = n.CreatedUtc,
                ReadUtc = n.IsGlobal ? (r != null ? r.ReadUtc : null) : n.ReadUtc,
                IsRead = n.IsGlobal ? r != null : n.ReadUtc.HasValue,
                Source = n.Source,
                Category = n.Category,
                TenantId = n.TenantId,
                Reference = n.Reference,
                CorrelationId = n.CorrelationId,
                Link = n.Link,
                IsGlobal = n.IsGlobal
            };

        return await query.Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<int> CountNotificationsForUserAsync(
        string userId,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        return BuildNotificationsQuery(userId, searchTerm, severity, source, category, from, to).CountAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUnreadNotificationsAsync(string userId, int skip, int take, CancellationToken ct)
    {
        var query =
            from n in BuildUnreadNotificationsQuery(userId)
            select new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                CreatedUtc = n.CreatedUtc,
                ReadUtc = null,
                IsRead = false,
                Source = n.Source,
                Category = n.Category,
                TenantId = n.TenantId,
                Reference = n.Reference,
                CorrelationId = n.CorrelationId,
                Link = n.Link,
                IsGlobal = n.IsGlobal
            };

        return await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUnreadErrorNotificationsAsync(string userId, int take, CancellationToken ct)
    {
        var query =
            from n in BuildUnreadNotificationsQuery(userId)
            where n.Severity == NotificationSeverity.Error || n.Severity == NotificationSeverity.Critical
            select new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                CreatedUtc = n.CreatedUtc,
                ReadUtc = null,
                IsRead = false,
                Source = n.Source,
                Category = n.Category,
                TenantId = n.TenantId,
                Reference = n.Reference,
                CorrelationId = n.CorrelationId,
                Link = n.Link,
                IsGlobal = n.IsGlobal
            };

        return await query
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountUnreadErrorNotificationsAsync(string userId, CancellationToken ct)
    {
        return BuildUnreadNotificationsQuery(userId)
            .CountAsync(n => n.Severity == NotificationSeverity.Error || n.Severity == NotificationSeverity.Critical, ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetDomainEventTimelineAsync(
        string userId,
        string? reference,
        string? correlationId,
        string? tenantId,
        int take,
        CancellationToken ct)
    {
        var readsForUser = _context.NotificationReads
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var domainEventsQuery = _context.Notifications.AsNoTracking()
            .Where(n =>
                (n.IsGlobal || n.UserId == userId) &&
                n.Category == "DomainEvent");

        if (!string.IsNullOrWhiteSpace(reference))
            domainEventsQuery = domainEventsQuery.Where(n => n.Reference == reference);

        if (!string.IsNullOrWhiteSpace(correlationId))
            domainEventsQuery = domainEventsQuery.Where(n => n.CorrelationId == correlationId);

        if (!string.IsNullOrWhiteSpace(tenantId))
            domainEventsQuery = domainEventsQuery.Where(n => n.TenantId == tenantId);

        var query =
            from n in domainEventsQuery
            join r in readsForUser on n.Id equals r.NotificationId into readGroup
            from r in readGroup.DefaultIfEmpty()
            orderby n.CreatedUtc ascending
            select new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                CreatedUtc = n.CreatedUtc,
                ReadUtc = n.IsGlobal ? (r != null ? r.ReadUtc : null) : n.ReadUtc,
                IsRead = n.IsGlobal ? r != null : n.ReadUtc.HasValue,
                Source = n.Source,
                Category = n.Category,
                TenantId = n.TenantId,
                Reference = n.Reference,
                CorrelationId = n.CorrelationId,
                Link = n.Link,
                IsGlobal = n.IsGlobal
            };

        return await query.Take(take).ToListAsync(ct);
    }

    public async Task<NotificationSummaryDto> GetNotificationSummaryAsync(string userId, CancellationToken ct)
    {
        var totalCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.IsGlobal || x.UserId == userId, ct);

        var unreadPersonalCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => !x.IsGlobal && x.UserId == userId && x.ReadUtc == null, ct);

        var unreadGlobalCount = await _context.Notifications
            .AsNoTracking()
            .Where(x => x.IsGlobal)
            .CountAsync(x => !_context.NotificationReads.Any(r => r.NotificationId == x.Id && r.UserId == userId), ct);

        var unreadErrorCount = await CountUnreadErrorNotificationsAsync(userId, ct);

        return new NotificationSummaryDto
        {
            TotalCount = totalCount,
            UnreadCount = unreadPersonalCount + unreadGlobalCount,
            UnreadErrorCount = unreadErrorCount
        };
    }

    public async Task<bool> MarkNotificationReadAsync(Guid notificationId, string userId, DateTime readUtc, CancellationToken ct)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId, ct);

        if (notification is null)
            return false;

        if (!notification.IsGlobal)
        {
            if (!string.Equals(notification.UserId, userId, StringComparison.Ordinal))
                return false;

            if (notification.ReadUtc.HasValue)
                return true;

            notification.ReadUtc = readUtc;
            await _context.SaveChangesAsync(ct);
            return true;
        }

        var existingRead = await _context.NotificationReads
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, ct);

        if (existingRead is not null)
            return true;

        _context.NotificationReads.Add(new NotificationReadEntity
        {
            NotificationId = notificationId,
            UserId = userId,
            ReadUtc = readUtc
        });

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkNotificationsReadAsync(IReadOnlyCollection<Guid> notificationIds, string userId, DateTime readUtc, CancellationToken ct)
    {
        if (notificationIds.Count == 0)
            return 0;

        var ids = notificationIds.Distinct().ToArray();

        var notifications = await _context.Notifications
            .Where(n => ids.Contains(n.Id) && (n.IsGlobal || n.UserId == userId))
            .ToListAsync(ct);

        if (notifications.Count == 0)
            return 0;

        var notificationIdsSet = notifications.Select(n => n.Id).ToHashSet();
        var existingGlobalReads = await _context.NotificationReads
            .Where(r => r.UserId == userId && notificationIdsSet.Contains(r.NotificationId))
            .Select(r => r.NotificationId)
            .ToListAsync(ct);

        var existingGlobalReadSet = existingGlobalReads.ToHashSet();
        var updated = 0;

        foreach (var notification in notifications)
        {
            if (!notification.IsGlobal)
            {
                if (notification.ReadUtc.HasValue)
                    continue;

                notification.ReadUtc = readUtc;
                updated++;
                continue;
            }

            if (existingGlobalReadSet.Contains(notification.Id))
                continue;

            _context.NotificationReads.Add(new NotificationReadEntity
            {
                NotificationId = notification.Id,
                UserId = userId,
                ReadUtc = readUtc
            });
            updated++;
        }

        if (updated > 0)
            await _context.SaveChangesAsync(ct);

        return updated;
    }

    private IQueryable<NotificationEntity> BuildNotificationsQuery(
        string userId,
        string? searchTerm,
        NotificationSeverity? severity,
        string? source,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var query = _context.Notifications.AsNoTracking().Where(n => n.IsGlobal || n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(n =>
                EF.Functions.ILike(n.Title, $"%{term}%") ||
                EF.Functions.ILike(n.Message, $"%{term}%") ||
                (n.Reference != null && EF.Functions.ILike(n.Reference, $"%{term}%")) ||
                (n.CorrelationId != null && EF.Functions.ILike(n.CorrelationId, $"%{term}%")));
        }

        if (severity.HasValue)
            query = query.Where(n => n.Severity == severity.Value);

        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(n => n.Source == source);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(n => n.Category == category);

        if (from.HasValue)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(n => n.CreatedUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(n => n.CreatedUtc <= toUtc);
        }

        return query;
    }

    private IQueryable<NotificationEntity> BuildUnreadNotificationsQuery(string userId)
    {
        var readsForUser = _context.NotificationReads
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        return _context.Notifications.AsNoTracking()
            .Where(n =>
                (n.IsGlobal && !readsForUser.Any(r => r.NotificationId == n.Id))
                || (!n.IsGlobal && n.UserId == userId && n.ReadUtc == null))
            .OrderByDescending(n => n.CreatedUtc);
    }

    private static IQueryable<NotificationEntity> ApplySorting(
        IQueryable<NotificationEntity> query,
        string? sortBy,
        string? sortDir)
    {
        var isAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "created" => isAsc
                ? query.OrderBy(n => n.CreatedUtc)
                : query.OrderByDescending(n => n.CreatedUtc),

            "severity" => isAsc
                ? query.OrderBy(n => n.Severity)
                : query.OrderByDescending(n => n.Severity),

            "source" => isAsc
                ? query.OrderBy(n => n.Source)
                : query.OrderByDescending(n => n.Source),

            "category" => isAsc
                ? query.OrderBy(n => n.Category)
                : query.OrderByDescending(n => n.Category),

            _ => query.OrderByDescending(n => n.CreatedUtc)
        };
    }
}
