using System.Collections.Concurrent;
using System.Threading.Channels;
using Helpdesk.Application.Timeline;
using Helpdesk.Shared.DTOs.Worklog;

namespace Helpdesk.Infrastructure.Services;

public sealed class TimelineEventBus : ITimelineEventBus
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<TicketTimelineEventDto>>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public ChannelReader<TicketTimelineEventDto> Subscribe(string ticketId)
    {
        var subscribers = _subscriptions.GetOrAdd(ticketId, _ => new ConcurrentDictionary<Guid, Channel<TicketTimelineEventDto>>());
        var subscriptionId = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<TicketTimelineEventDto>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        subscribers[subscriptionId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(string ticketId, ChannelReader<TicketTimelineEventDto> reader)
    {
        if (!_subscriptions.TryGetValue(ticketId, out var subscribers))
            return;

        foreach (var pair in subscribers)
        {
            if (!ReferenceEquals(pair.Value.Reader, reader))
                continue;

            if (subscribers.TryRemove(pair.Key, out var removed))
            {
                removed.Writer.TryComplete();
            }

            break;
        }

        if (subscribers.IsEmpty)
        {
            _subscriptions.TryRemove(ticketId, out _);
        }
    }

    public ValueTask PublishAsync(TicketTimelineEventDto evt)
    {
        if (!_subscriptions.TryGetValue(evt.TicketId, out var subscribers))
            return ValueTask.CompletedTask;

        foreach (var pair in subscribers)
        {
            var writer = pair.Value.Writer;
            if (!writer.TryWrite(evt) && subscribers.TryRemove(pair.Key, out var removed))
            {
                removed.Writer.TryComplete();
            }
        }

        if (subscribers.IsEmpty)
        {
            _subscriptions.TryRemove(evt.TicketId, out _);
        }

        return ValueTask.CompletedTask;
    }
}
