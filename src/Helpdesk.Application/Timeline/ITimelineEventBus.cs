using System.Threading.Channels;
using Helpdesk.Shared.DTOs.Worklog;

namespace Helpdesk.Application.Timeline;

public interface ITimelineEventBus
{
    ChannelReader<TicketTimelineEventDto> Subscribe(string ticketId);
    void Unsubscribe(string ticketId, ChannelReader<TicketTimelineEventDto> reader);
    ValueTask PublishAsync(TicketTimelineEventDto evt);
}
