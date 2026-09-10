using System.Security.Claims;
using Helpdesk.Application.Dashboard;
using Helpdesk.Application.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/admin-summary", async ([FromServices] IRequestSender sender) =>
            await sender.Send(new GetAdminDashboardQuery()))
        .WithName("GetAdminDashboard")
        .WithSummary("Admin dashboard metrics.")
        .WithDescription("Returns live counts for incidents, requests, and change requests.")
        .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/technician-summary", async ([FromServices] IRequestSender sender, ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await sender.Send(new GetTechnicianDashboardQuery(id));
        })
        .WithName("GetTechnicianDashboard")
        .WithSummary("Technician dashboard metrics.")
        .WithDescription("Returns open counts for the authenticated technician.");

        group.MapGet("/customer-summary", async ([FromServices] IRequestSender sender, ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await sender.Send(new GetCustomerDashboardQuery(id));
        })
        .WithName("GetCustomerDashboard")
        .WithSummary("Customer dashboard metrics.")
        .WithDescription("Returns ticket counts for the authenticated customer.");
    }
}
