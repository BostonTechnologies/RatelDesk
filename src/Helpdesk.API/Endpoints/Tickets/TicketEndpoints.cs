using System.Security.Claims;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Tickets;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketRequest = Helpdesk.Shared.Models.Request;

namespace Helpdesk.API.Endpoints.Tickets;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tickets").WithTags("Tickets").RequireAuthorization();
        var ticketGroup = app.MapGroup("/api/v1/{ticketType}/{ticketId}").WithTags("Tickets").RequireAuthorization();

        ticketGroup.MapGet("/timeline/count", async (string ticketType, string ticketId, HttpContext context, ICurrentUserAccessService access, HelpdeskDbContext db, CancellationToken ct) =>
        {
            var failure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, access, db, ct);
            return failure ?? Results.Ok(await db.TicketTimelineEvents.AsNoTracking().CountAsync(x => x.TicketId == ticketId, ct));
        });

        ticketGroup.MapGet("/attachments/count", async (string ticketType, string ticketId, HttpContext context, ICurrentUserAccessService access, HelpdeskDbContext db, CancellationToken ct) =>
        {
            var failure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, access, db, ct);
            return failure ?? Results.Ok(await db.Attachments.AsNoTracking().CountAsync(x => x.TicketId == ticketId, ct));
        });

        ticketGroup.MapGet("/listeners/count", async (string ticketType, string ticketId, HttpContext context, ICurrentUserAccessService access, HelpdeskDbContext db, CancellationToken ct) =>
        {
            var failure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, access, db, ct);
            if (failure is not null) return failure;
            var count = ticketType.ToLowerInvariant() switch
            {
                "incidents" => await db.Incidents.AsNoTracking().Where(x => x.Id == ticketId).Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count).SingleOrDefaultAsync(ct),
                "requests" => await db.Requests.AsNoTracking().Where(x => x.Id == ticketId).Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count).SingleOrDefaultAsync(ct),
                "changes" => await db.Changes.AsNoTracking().Where(x => x.Id == ticketId).Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count).SingleOrDefaultAsync(ct),
                _ => 0
            };
            return Results.Ok(count);
        });

        MapTicketCustomerEndpoint(ticketGroup);

        group.MapPost("/{ticketId}/mark-as-seen", async (string ticketId, HttpContext context, ICurrentUserAccessService access, HelpdeskDbContext db, IRequestSender sender, CancellationToken ct) =>
        {
            var failure = await AuthorizeTicketViewAsync("incidents", ticketId, context.User, access, db, ct);
            if (failure is not null) return failure;
            await sender.Send(new MarkTicketAsSeenCommand(ticketId), ct);
            return Results.NoContent();
        }).WithName("MarkTicketAsSeen").WithSummary("Marks a ticket as viewed");
    }

    public static RouteHandlerBuilder MapTicketCustomerEndpoint(RouteGroupBuilder ticketGroup) =>
        ticketGroup.MapPost("/customer", async ([FromRoute] string ticketType, [FromRoute] string ticketId, [FromBody] UpdateTicketCustomerRequest request, IRepository<Incident> incidents, IRepository<TicketRequest> requests, IRepository<Change> changes, IRepository<Customer> customers, ICurrentUserAccessService access, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var ticket = await GetTicketAsync(ticketType, ticketId, incidents, requests, changes);
            if (ticket is null) return Results.NotFound();
            var failure = await AuthorizeTicketManageAsync(ticket, user, access, ct);
            if (failure is not null) return failure;
            if (!string.IsNullOrWhiteSpace(ticket.CustomerId)) return Results.BadRequest("Ticket already has a requester.");
            var customer = await customers.GetAsync(request.CustomerId ?? string.Empty);
            if (customer is null || !customer.IsEnabled || !string.Equals(customer.OrganizationId, ticket.OrganizationId, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Customer must be enabled and belong to the selected organization.");
            ticket.CustomerId = customer.Id;
            ticket.RequesterEmail = customer.Email;
            ticket.UpdatedAt = DateTime.UtcNow;
            var updated = await UpdateTicketAsync(ticketType, ticket, incidents, requests, changes);
            return updated ? Results.Ok(new { customer.Id, customer.Name, customer.Email }) : Results.NotFound();
        }).WithName("SetTicketCustomer").WithSummary("Set the requester customer for a ticket");

    internal static async Task<IResult?> AuthorizeTicketViewAsync(string ticketType, string ticketId, ClaimsPrincipal user, ICurrentUserAccessService accessService, HelpdeskDbContext db, CancellationToken ct)
    {
        if (ticketType.ToLowerInvariant() is not ("incidents" or "requests" or "changes")) return Results.BadRequest("Unsupported ticketType. Use incidents, requests, or changes.");
        var ticket = await db.Tickets.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct);
        if (ticket is null) return Results.NotFound();
        var profile = await accessService.ResolveAsync(user, ct);
        var canView = ticket switch
        {
            Incident => profile.CanViewIncident(ticket.OrganizationId, ticket.CustomerId, ticket.RequesterEmail),
            TicketRequest => profile.CanViewRequest(ticket.OrganizationId, ticket.CustomerId, ticket.RequesterEmail),
            Change => profile.CanViewChange(ticket.OrganizationId, ticket.CustomerId, ticket.RequesterEmail),
            _ => false
        };
        return canView ? null : Results.Forbid();
    }

    internal static async Task<IResult?> AuthorizeTicketManageAsync(string ticketId, ClaimsPrincipal user, ICurrentUserAccessService accessService, HelpdeskDbContext db, CancellationToken ct)
    {
        var ticket = await db.Tickets.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct);
        return ticket is null ? Results.NotFound() : await AuthorizeTicketManageAsync(ticket, user, accessService, ct);
    }

    private static async Task<IResult?> AuthorizeTicketManageAsync(Ticket ticket, ClaimsPrincipal user, ICurrentUserAccessService accessService, CancellationToken ct)
    {
        var profile = await accessService.ResolveAsync(user, ct);
        return ticket switch
        {
            Incident when profile.CanManageIncident(ticket.OrganizationId) => null,
            TicketRequest when profile.CanManageRequest(ticket.OrganizationId) => null,
            Change when profile.CanManageChange(ticket.OrganizationId) => null,
            _ => Results.Forbid()
        };
    }

    private static async Task<Ticket?> GetTicketAsync(string type, string id, IRepository<Incident> incidents, IRepository<TicketRequest> requests, IRepository<Change> changes) => type.ToLowerInvariant() switch
    {
        "incidents" => await incidents.GetAsync(id),
        "requests" => await requests.GetAsync(id),
        "changes" => await changes.GetAsync(id),
        _ => null
    };

    private static async Task<bool> UpdateTicketAsync(string type, Ticket ticket, IRepository<Incident> incidents, IRepository<TicketRequest> requests, IRepository<Change> changes) => type.ToLowerInvariant() switch
    {
        "incidents" when ticket is Incident incident => await incidents.UpdateAsync(incident) is not null,
        "requests" when ticket is TicketRequest request => await requests.UpdateAsync(request) is not null,
        "changes" when ticket is Change change => await changes.UpdateAsync(change) is not null,
        _ => false
    };

    public sealed record UpdateTicketCustomerRequest(string? CustomerId);
}
