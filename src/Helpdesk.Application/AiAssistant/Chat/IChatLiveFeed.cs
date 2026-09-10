using System.Threading.Channels;
using Helpdesk.Shared.AiAssistant.Chat;

namespace Helpdesk.Application.AiAssistant.Chat;

public interface IChatLiveFeed
{
    ChannelReader<ChatDelta> Subscribe(Guid conversation);
    void Unsubscribe(Guid conversation, ChannelReader<ChatDelta> reader);
    void Publish(ChatDelta delta);
}
