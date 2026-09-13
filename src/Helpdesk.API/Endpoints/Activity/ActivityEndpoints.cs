using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        app.MapGet("/api/v1/incidents/{id}/activity", async (
            [FromRoute] string id,
            [FromServices] HelpdeskDbContext db,
            [FromServices] ICurrentUserAccessService accessService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var incident = await db.Incidents.IgnoreQueryFilters().AsNoTracking()
                .Where(candidate => candidate.Id == id)
                .Select(candidate => new { candidate.OrganizationId, candidate.CustomerId, candidate.RequesterEmail })
                .FirstOrDefaultAsync(cancellationToken);
            if (incident is null)
            {
                return Results.NotFound();
            }

            var customer = !string.IsNullOrWhiteSpace(incident.CustomerId)
                ? await db.Customers.AsNoTracking()
                    .Where(candidate => candidate.Id == incident.CustomerId)
                    .Select(candidate => new { candidate.Id, candidate.Email })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var access = await accessService.ResolveAsync(context.User, cancellationToken);
            if (!access.CanViewIncident(
                    incident.OrganizationId,
                    customer?.Id ?? incident.CustomerId,
                    customer?.Email ?? incident.RequesterEmail))
            {
                return Results.Forbid();
            }

            var activity = await db.ActivityLogs.IgnoreQueryFilters().AsNoTracking()
                .Where(log => log.TicketId == id)
                .OrderBy(log => log.Timestamp)
                .ToListAsync(cancellationToken);
            return Results.Ok(activity);
        })
            .RequireAuthorization()
            .WithName("GetIncidentActivity")
            .WithSummary("Incident activity log")
            .WithDescription("Returns log entries for the specified incident.");
    }
}
