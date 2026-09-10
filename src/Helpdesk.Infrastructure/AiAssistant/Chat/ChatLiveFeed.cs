using System.Collections.Concurrent;
using System.Threading.Channels;
using Helpdesk.Application.AiAssistant.Chat;
using Helpdesk.Shared.AiAssistant.Chat;

namespace Helpdesk.Infrastructure.AiAssistant.Chat;

public sealed class ChatLiveFeed : IChatLiveFeed
{
    private readonly ConcurrentDictionary<ChannelReader<ChatDelta>, (Guid Id, Channel<ChatDelta> Channel)> subscriptions = new();
    public ChannelReader<ChatDelta> Subscribe(Guid conversation)
    {
        var channel = Channel.CreateBounded<ChatDelta>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.DropOldest });
        subscriptions[channel.Reader] = (conversation, channel);
        return channel.Reader;
    }
    public void Unsubscribe(Guid conversation, ChannelReader<ChatDelta> reader)
    {
        if (subscriptions.TryRemove(reader, out var entry)) entry.Channel.Writer.TryComplete();
    }
    public void Publish(ChatDelta delta)
    {
        foreach (var entry in subscriptions.Values)
            if (entry.Id == delta.ConversationId) entry.Channel.Writer.TryWrite(delta);
    }
}
