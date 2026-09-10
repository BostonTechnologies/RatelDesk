using System.Threading.Channels;
using Helpdesk.Shared.DTOs.Notification;

namespace Helpdesk.Application.Notifications;

public interface INotificationEventBus
{
    ChannelReader<NotificationDto> Subscribe(string? tenantId);
    void Publish(NotificationDto notification);
    void Unsubscribe(string? tenantId, ChannelReader<NotificationDto> reader);
}
