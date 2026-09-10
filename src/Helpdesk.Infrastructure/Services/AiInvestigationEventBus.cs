using System.Collections.Concurrent;
using System.Threading.Channels;
using Helpdesk.Application.AiAssistant;
using Helpdesk.Shared.DTOs;

namespace Helpdesk.Infrastructure.Services;

public sealed class AiInvestigationEventBus : IAiInvestigationEventBus
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<AiInvestigationWorklogEntryDto>>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    public ChannelReader<AiInvestigationWorklogEntryDto> Subscribe(string ticketId)
    {
        var channel = Channel.CreateUnbounded<AiInvestigationWorklogEntryDto>();
        _subscriptions.GetOrAdd(ticketId, _ => new())[Guid.NewGuid()] = channel;
        return channel.Reader;
    }
    public void Unsubscribe(string ticketId, ChannelReader<AiInvestigationWorklogEntryDto> reader)
    {
        if (!_subscriptions.TryGetValue(ticketId, out var subscribers)) return;
        foreach (var item in subscribers.Where(x => ReferenceEquals(x.Value.Reader, reader)).ToArray())
            if (subscribers.TryRemove(item.Key, out var channel)) channel.Writer.TryComplete();
        if (subscribers.IsEmpty) _subscriptions.TryRemove(ticketId, out _);
    }
    public ValueTask PublishAsync(AiInvestigationWorklogEntryDto entry)
    {
        if (_subscriptions.TryGetValue(entry.InvocationId.ToString(), out _)) { }
        // Worklog subscribers are ticket keyed; the service publishes with the ticket key through this overload's envelope.
        return ValueTask.CompletedTask;
    }
    public ValueTask PublishAsync(string ticketId, AiInvestigationWorklogEntryDto entry)
    {
        if (_subscriptions.TryGetValue(ticketId, out var subscribers)) foreach (var channel in subscribers.Values) channel.Writer.TryWrite(entry);
        return ValueTask.CompletedTask;
    }
}
