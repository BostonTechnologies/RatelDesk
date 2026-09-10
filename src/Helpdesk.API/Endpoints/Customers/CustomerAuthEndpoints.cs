using System.Security.Claims;
using Helpdesk.Infrastructure.Auth.Authentik;
using Microsoft.AspNetCore.Mvc;

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
            CancellationToken ct) =>
            Results.Ok(await invitationService.GetStatusAsync(customerId, ct)));

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
            CancellationToken ct) =>
            Results.Ok(await invitationService.GetStatusAsync(customerId, ct)));

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

    private static string ResolveUserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? user.FindFirstValue("sub")
           ?? user.FindFirstValue("preferred_username")
           ?? user.Identity?.Name
           ?? "unknown";
}
