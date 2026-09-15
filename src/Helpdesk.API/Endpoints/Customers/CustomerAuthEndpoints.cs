using System.Security.Claims;
using Helpdesk.Infrastructure.Auth.Authentik;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Customer;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Customers;

public static class CustomerAuthEndpoints
{
    public static void MapCustomerAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers/{customerId}/auth")
            .WithTags("Customer Authentication")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/status", async (
            [FromRoute] string customerId,
            [FromServices] ICustomerInvitationService invitationService,
            [FromServices] HelpdeskDbContext db,
            [FromServices] UserManager<ApplicationUser> users,
            CancellationToken ct) =>
            Results.Ok(await GetStatusAsync(customerId, invitationService, db, users, ct)));

        group.MapPost("/invite", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.InviteAsync(customerId, ResolveUserId(user), ct)));

        group.MapPost("/resend-invite", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.ResendInviteAsync(customerId, ResolveUserId(user), ct)));

        group.MapPost("/disable-login", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.DisableLoginAsync(customerId, ResolveUserId(user), ct)));

        group.MapPost("/sync-authentik", async (
            [FromRoute] string customerId,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.SyncAuthentikAsync(customerId, ct)));

        var adminGroup = app.MapGroup("/api/admin/customers/{customerId}")
            .WithTags("Customer Authentication")
            .RequireAuthorization("HelpdeskAdmin");

        adminGroup.MapGet("/auth-status", async (
            [FromRoute] string customerId,
            [FromServices] ICustomerInvitationService invitationService,
            [FromServices] HelpdeskDbContext db,
            [FromServices] UserManager<ApplicationUser> users,
            CancellationToken ct) =>
            Results.Ok(await GetStatusAsync(customerId, invitationService, db, users, ct)));

        adminGroup.MapPost("/invite", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.InviteAsync(customerId, ResolveUserId(user), ct)));

        adminGroup.MapPost("/resend-invite", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.ResendInviteAsync(customerId, ResolveUserId(user), ct)));

        adminGroup.MapPost("/disable-login", async (
            [FromRoute] string customerId,
            ClaimsPrincipal user,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.DisableLoginAsync(customerId, ResolveUserId(user), ct)));

        adminGroup.MapPost("/sync-authentik", async (
            [FromRoute] string customerId,
            [FromServices] ICustomerInvitationService invitationService,
            CancellationToken ct) =>
            await RunCustomerAuthActionAsync(() => invitationService.SyncAuthentikAsync(customerId, ct)));
    }

    private static async Task<IResult> RunCustomerAuthActionAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (AuthentikAdminConfigurationException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Customer invitation is not configured");
        }
        catch (AuthentikAdminRequestException ex)
        {
            var statusCode = (int)ex.StatusCode is >= 400 and <= 499
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status502BadGateway;
            return Results.Problem(
                detail: ex.Message,
                statusCode: statusCode,
                title: "Customer invitation request could not be completed");
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Customer invitation request could not be completed");
        }
    }

    private static async Task<CustomerAuthStatusDto> GetStatusAsync(
        string customerId,
        ICustomerInvitationService invitationService,
        HelpdeskDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, ct)
            ?? throw new InvalidOperationException("Customer not found.");
        var links = await db.CustomerAuthLinks.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.AuthProviderType)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        if (links.Count == 0)
        {
            return NoLinkedLogin(customer.Id);
        }

        if (links.Count > 1)
        {
            return new CustomerAuthStatusDto
            {
                CustomerId = customer.Id,
                InviteStatus = CustomerInviteStatus.Failed,
                StatusText = "Multiple linked identities",
                IdentitySummary = "Multiple provider links require instance-administrator review.",
                HasMultipleIdentityLinks = true,
                IsLinkedLogin = true,
                LastAuthError = "Access actions are unavailable until the linked identities are reviewed.",
                AuthProviderType = string.Join(", ", links.Select(link => link.AuthProviderType).Distinct(StringComparer.OrdinalIgnoreCase))
            };
        }

        var link = links[0];
        if (string.Equals(link.AuthProviderType, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(link.LocalAccountId))
        {
            return await GetLocalAccountStatusAsync(customer.Id, link, users);
        }

        // Existing external flows remain owned by the invitation provider. The
        // caller receives its exact provider/link status rather than an inferred
        // identity from a matching customer email address.
        return await invitationService.GetStatusAsync(customerId, ct);
    }

    private static async Task<CustomerAuthStatusDto> GetLocalAccountStatusAsync(
        string customerId,
        CustomerAuthLink link,
        UserManager<ApplicationUser> users)
    {
        var account = await users.FindByIdAsync(link.LocalAccountId!);
        var status = account switch
        {
            null => CustomerInviteStatus.Failed,
            { IsEnabled: false } => CustomerInviteStatus.Disabled,
            { EmailConfirmed: false } or { PasswordHash: null or "" } => CustomerInviteStatus.Pending,
            _ => CustomerInviteStatus.Active
        };

        return new CustomerAuthStatusDto
        {
            CustomerId = customerId,
            InviteStatus = status,
            StatusText = status switch
            {
                CustomerInviteStatus.Pending => "Activation pending",
                CustomerInviteStatus.Active => "Login active",
                CustomerInviteStatus.Disabled => "Login disabled",
                _ => "Linked local account unavailable"
            },
            AuthProviderType = "Local",
            LocalAccountId = link.LocalAccountId,
            DomainUserId = link.DomainUserId,
            IdentitySummary = account is null ? "The linked local account no longer exists." : account.Email,
            IsLinkedLogin = true,
            DisabledAtUtc = account?.DisabledAtUtc,
            LastAuthError = account is null ? "The linked local account could not be found." : null,
            CanInvite = false,
            CanResend = false,
            CanDisableLogin = false
        };
    }

    private static CustomerAuthStatusDto NoLinkedLogin(string customerId) => new()
    {
        CustomerId = customerId,
        InviteStatus = CustomerInviteStatus.NotInvited,
        StatusText = "No linked login",
        IdentitySummary = "This contact has no login link.",
        CanInvite = true
    };

    private static string ResolveUserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? user.FindFirstValue("sub")
           ?? user.FindFirstValue("preferred_username")
           ?? user.Identity?.Name
           ?? "unknown";
}
