using System.Security.Claims;
using Helpdesk.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace Helpdesk.API.Hubs;

public class NotificationHub(IUserPresenceService presence) : Hub
{
    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            presence.AddConnection(userId, Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            presence.RemoveConnection(userId, Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
