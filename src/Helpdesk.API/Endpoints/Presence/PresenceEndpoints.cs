using Helpdesk.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Presence;

public static class PresenceEndpoints
{
    public static void MapPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/presence")
            .WithTags("Presence")
            .RequireAuthorization();

        group.MapGet("/online-users", ([FromServices] IUserPresenceService presence) => Results.Ok(presence.GetOnlineUsers()))
            .WithName("GetOnlineUsers")
            .WithSummary("Lists online users.")
            .WithDescription("Returns user IDs for currently connected users.");
    }
}
