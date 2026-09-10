using System.Security.Claims;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Customer;
using Helpdesk.Shared.DTOs.Organization;
using Helpdesk.Shared.DTOs.User;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Ticketing;

public static class TicketingLookupEndpoints
{
    public static void MapTicketingLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ticketing")
            .WithTags("Ticketing")
            .RequireAuthorization();

        group.MapGet("/organizations", async (
            ClaimsPrincipal user,
            [FromServices] HelpdeskDbContext db,
            [FromServices] ICurrentUserAccessService accessService,
            CancellationToken token) =>
        {
            var access = await accessService.ResolveAsync(user, token);
            var organizationIds = access.AllowedOrganizationIds.ToArray();

            var query = db.Organizations.AsNoTracking()
                .Where(x => x.State == Helpdesk.Shared.Models.EntityState.Enabled);

            if (!access.IsHelpdeskAdmin)
            {
                query = query.Where(x => organizationIds.Contains(x.Id));
            }

            var items = await query
                .OrderBy(x => x.Name)
                .Select(x => new OrganizationDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    DnsName = x.DnsName,
                    ContactInfo = x.ContactInfo,
                    EnableAiIntake = x.EnableAiIntake,
                    ItSupportOrganizationId = x.ItSupportOrganizationId,
                    OrchestrationTenantId = x.OrchestrationTenantId,
                    OrchestrationTenantName = x.OrchestrationTenantName,
                    OrchestrationTenantLinkedAtUtc = x.OrchestrationTenantLinkedAtUtc,
                    State = x.State
                })
                .ToListAsync(token);

            return Results.Ok(items);
        })
        .WithName("GetTicketingOrganizations");

        group.MapGet("/customers", async (
            ClaimsPrincipal user,
            [FromQuery] string? organizationId,
            [FromServices] HelpdeskDbContext db,
            [FromServices] ICurrentUserAccessService accessService,
            CancellationToken token) =>
        {
            var access = await accessService.ResolveAsync(user, token);
            if (!CanUseOrganization(access, organizationId))
            {
                return Results.Forbid();
            }

            var query =
                from customer in db.Customers.AsNoTracking()
                join organization in db.Organizations.AsNoTracking() on customer.OrganizationId equals organization.Id
                where customer.State == Helpdesk.Shared.Models.EntityState.Enabled &&
                      organization.State == Helpdesk.Shared.Models.EntityState.Enabled &&
                      customer.OrganizationId == organizationId
                select new { Customer = customer, OrganizationName = organization.Name };

            if (!access.IsHelpdeskAdmin && !IsManager(access, organizationId))
            {
                query = query.Where(x => x.Customer.Id == access.CustomerId);
            }

            var items = await query
                .OrderBy(x => x.Customer.Name)
                .ThenBy(x => x.Customer.Email)
                .Select(x => new CustomerDto
                {
                    Id = x.Customer.Id,
                    Name = x.Customer.Name,
                    Email = x.Customer.Email,
                    OrganizationId = x.Customer.OrganizationId,
                    OrganizationName = x.OrganizationName,
                    State = x.Customer.State
                })
                .ToListAsync(token);

            return Results.Ok(items);
        })
        .WithName("GetTicketingCustomers");

        group.MapGet("/assignees", async (
            ClaimsPrincipal user,
            [FromQuery] string? organizationId,
            [FromServices] HelpdeskDbContext db,
            [FromServices] ICurrentUserAccessService accessService,
            CancellationToken token) =>
        {
            var access = await accessService.ResolveAsync(user, token);
            if (!CanUseOrganization(access, organizationId))
            {
                return Results.Forbid();
            }

            if (!access.IsHelpdeskAdmin && !IsManager(access, organizationId))
            {
                return Results.Ok(Array.Empty<UserDto>());
            }

            var users = await db.Users.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Email)
                .Select(x => new UserDto(x.Id, x.Name, x.Email, x.Role, x.IsTestUser, x.OrganizationId))
                .ToListAsync(token);

            return Results.Ok(users);
        })
        .WithName("GetTicketingAssignees");
    }

    private static bool CanUseOrganization(CurrentUserAccessProfile access, string? organizationId) =>
        !string.IsNullOrWhiteSpace(organizationId) &&
        (access.IsHelpdeskAdmin || access.AllowedOrganizationIds.Contains(organizationId));

    private static bool IsManager(CurrentUserAccessProfile access, string? organizationId) =>
        access.CanManageIncident(organizationId) ||
        access.CanManageRequest(organizationId) ||
        access.CanManageChange(organizationId);
}
