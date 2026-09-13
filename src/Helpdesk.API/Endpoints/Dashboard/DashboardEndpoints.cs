using System.Security.Claims;
using Helpdesk.Application.Dashboard;
using Helpdesk.Application.Messaging;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;
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

        group.MapGet("/technician-summary", async (
            [FromServices] IRequestSender sender,
            [FromServices] ICurrentUserAccessService accessService,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var access = await accessService.ResolveAsync(user, token);
            return await sender.Send(new GetTechnicianDashboardQuery(
                id,
                access.OrganizationIdsForAny(HelpdeskPermissions.IncidentRead, HelpdeskPermissions.IncidentWrite, HelpdeskPermissions.IncidentManager),
                access.OrganizationIdsForAny(HelpdeskPermissions.ChangeRead, HelpdeskPermissions.ChangeWrite, HelpdeskPermissions.ChangeManager),
                access.IsHelpdeskAdmin), token);
        })
        .WithName("GetTechnicianDashboard")
        .WithSummary("Technician dashboard metrics.")
        .WithDescription("Returns scoped open counts for the authenticated technician.")
        .RequireAuthorization("HelpdeskStaff");

        group.MapGet("/customer-summary", async (
            [FromServices] IRequestSender sender,
            [FromServices] ICurrentUserAccessService accessService,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var access = await accessService.ResolveAsync(user, token);
            return string.IsNullOrWhiteSpace(access.CustomerId)
                ? new CustomerDashboardDto(0, 0)
                : await sender.Send(new GetCustomerDashboardQuery(access.CustomerId,
                    access.IsHelpdeskAdmin ? null : access.OrganizationIdsFor(HelpdeskPermissions.IncidentUser)), token);
        })
        .WithName("GetCustomerDashboard")
        .WithSummary("Customer dashboard metrics.")
        .WithDescription("Returns ticket counts for the authenticated customer.")
        .RequireAuthorization(HelpdeskPermissions.SelfServiceUser);
    }
}
