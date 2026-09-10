using Helpdesk.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Helpdesk.API.Services;

public class SignalRNotificationService(IHubContext<NotificationHub> hub)
{
    public Task NotifyUserAsync(string userId, string message, string ticketId)
    {
        return hub.Clients.User(userId).SendAsync(message, ticketId);
    }
}
