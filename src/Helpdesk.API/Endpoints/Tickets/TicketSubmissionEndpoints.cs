using Helpdesk.Infrastructure.Services;
using Helpdesk.Application.Incidents;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Services.Notifications;
using Helpdesk.Application.Services.Tenants;
using Helpdesk.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Tickets;

public static class TicketSubmissionEndpoints
{
    public static void MapTicketSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        var systemPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes("System")
            .RequireAuthenticatedUser()
            .RequireRole("system.blazor-web")
            .Build();

        // PUBLIC endpoint for portal users
        var publicGroup = app.MapGroup("/api/v1/public/tickets")
            .WithTags("Public Tickets")
            .AllowAnonymous()
            .RequireRateLimiting("TicketSubmission");

        publicGroup.MapPost("/submit", async (
            [FromBody] SubmitTicketRequest dto,
            ICaptchaService captcha,
            ITenantProvisioningService tenantProvisioningService,
            ITicketNotificationService ticketNotificationService,
            IRequestSender sender) =>
        {
            if (!captcha.ValidateCaptcha(dto.CaptchaId, dto.CaptchaAnswer))
                return Results.BadRequest("Invalid captcha");

            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return Results.BadRequest("Invalid email address.");

            var domain = dto.Email.Split('@').Last();
            var organization = await tenantProvisioningService.GetOrCreateOrganizationByDomainAsync(domain);
            var (customer, _) = await tenantProvisioningService.GetOrCreateCustomerAsync(
                dto.Email,
                dto.Name,
                domain);

            var cmd = new CreateIncidentCommand(
                dto.Subject,
                dto.Message,
                dto.Priority,
                customer.Id,
                organization.Id,
                null,
                null,
                null,
                null,
                customer.Email,
                null,
                null);

            var incident = await sender.Send(cmd);
            _ = await ticketNotificationService.SendNewTicketConfirmationAsync(
                incident,
                customer.Email,
                customer.Name);

            return Results.Ok(new { Id = incident.Id, TrackingId = incident.TrackingId });
        });

        // SYSTEM endpoint remains protected
        var systemGroup = app.MapGroup("/api/v1/tickets")
            .WithTags("System Tickets")
            .RequireAuthorization(systemPolicy);

        systemGroup.MapPost("/submit", async (
            [FromBody] SubmitTicketRequest dto,
            ICaptchaService captcha,
            ITenantProvisioningService tenantProvisioningService,
            ITicketNotificationService ticketNotificationService,
            IRequestSender sender) =>
        {
            if (!captcha.ValidateCaptcha(dto.CaptchaId, dto.CaptchaAnswer))
                return Results.BadRequest("Invalid captcha");

            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return Results.BadRequest("Invalid email address.");

            var domain = dto.Email.Split('@').Last();
            var organization = await tenantProvisioningService.GetOrCreateOrganizationByDomainAsync(domain);
            var (customer, _) = await tenantProvisioningService.GetOrCreateCustomerAsync(
                dto.Email,
                dto.Name,
                domain);

            var cmd = new CreateIncidentCommand(
                dto.Subject,
                dto.Message,
                dto.Priority,
                customer.Id,
                organization.Id,
                null,
                null,
                null,
                null,
                customer.Email,
                null,
                null);

            var incident = await sender.Send(cmd);
            _ = await ticketNotificationService.SendNewTicketConfirmationAsync(
                incident,
                customer.Email,
                customer.Name);

            return Results.Ok(new { Id = incident.Id, TrackingId = incident.TrackingId });
        });
    }
}
