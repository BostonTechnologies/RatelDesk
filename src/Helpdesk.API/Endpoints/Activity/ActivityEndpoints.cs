using Helpdesk.Application.ActivityLogs;
using Helpdesk.Application.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Activity;

public static class ActivityEndpoints
{
    /// <summary>
    /// Maps the endpoints related to incident activity to the application's routing system.
    /// </summary>
    /// <remarks>This method defines an endpoint for retrieving the activity log of a specific incident.  The
    /// endpoint requires authorization and provides metadata for name, summary, and description.</remarks>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> used to define the application's routing.</param>
    public static void MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/incidents/{id}/activity", async ([FromRoute] string id, [FromServices] IRequestSender sender) =>
            Results.Ok(await sender.Send(new GetIncidentActivityQuery(id))))
            .RequireAuthorization()
            .WithName("GetIncidentActivity")
            .WithSummary("Incident activity log")
            .WithDescription("Returns log entries for the specified incident.");
    }
}
