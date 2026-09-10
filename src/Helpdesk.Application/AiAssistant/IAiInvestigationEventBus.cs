using System.Threading.Channels;
using Helpdesk.Shared.DTOs;

namespace Helpdesk.Application.AiAssistant;

public interface IAiInvestigationEventBus
{
    ChannelReader<AiInvestigationWorklogEntryDto> Subscribe(string ticketId);
    void Unsubscribe(string ticketId, ChannelReader<AiInvestigationWorklogEntryDto> reader);
    ValueTask PublishAsync(AiInvestigationWorklogEntryDto entry);
}
