using System.Collections.Concurrent;
using System.Threading.Channels;
using Helpdesk.Application.Notifications;
using Helpdesk.Shared.DTOs.Notification;

namespace Helpdesk.Infrastructure.Events;

public sealed class NotificationEventBus : INotificationEventBus
{
    private const string GlobalKey = "__global__";
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<NotificationDto>>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public ChannelReader<NotificationDto> Subscribe(string? tenantId)
    {
        var key = NormalizeKey(tenantId);
        var subscribers = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Channel<NotificationDto>>());
        var subscriptionId = Guid.NewGuid();

        var channel = Channel.CreateBounded<NotificationDto>(new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        subscribers[subscriptionId] = channel;
        return channel.Reader;
    }

    public void Publish(NotificationDto notification)
    {
        PublishToKey(GlobalKey, notification);
        if (!string.IsNullOrWhiteSpace(notification.TenantId))
            PublishToKey(notification.TenantId, notification);
    }

    public void Unsubscribe(string? tenantId, ChannelReader<NotificationDto> reader)
    {
        var key = NormalizeKey(tenantId);
        if (!_subscriptions.TryGetValue(key, out var subscribers))
            return;

        foreach (var pair in subscribers)
        {
            if (!ReferenceEquals(pair.Value.Reader, reader))
                continue;

            if (subscribers.TryRemove(pair.Key, out var removed))
                removed.Writer.TryComplete();

            break;
        }

        if (subscribers.IsEmpty)
            _subscriptions.TryRemove(key, out _);
    }

    private void PublishToKey(string? key, NotificationDto notification)
    {
        var normalized = NormalizeKey(key);
        if (!_subscriptions.TryGetValue(normalized, out var subscribers))
            return;

        foreach (var pair in subscribers)
        {
            var writer = pair.Value.Writer;
            if (!writer.TryWrite(notification) && subscribers.TryRemove(pair.Key, out var removed))
                removed.Writer.TryComplete();
        }

        if (subscribers.IsEmpty)
            _subscriptions.TryRemove(normalized, out _);
    }

    private static string NormalizeKey(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? GlobalKey : tenantId.Trim();
    }
}
