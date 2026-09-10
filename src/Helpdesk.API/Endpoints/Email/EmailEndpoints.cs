using Helpdesk.API.Endpoints.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppServices = Helpdesk.Application.Services;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
namespace Helpdesk.API.Endpoints.Email;

public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/email")
            .WithTags("Email")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/imap/settings", async ([FromServices] HelpdeskDbContext db) =>
        {
            var settings = await db.ImapEmailSettings.FirstOrDefaultAsync();
            if (settings is null)
            {
                // If no settings exist yet, return a new default object.
                // This prevents the UI from breaking on the first run.
                return Results.Ok(new ImapEmailSettings());
            }
            return Results.Ok(settings);
        })
        .RequireAuthorization("HelpdeskAdmin")
        .WithName("GetImapSettings")
        .WithSummary("Retrieves the current IMAP settings")
        .WithDescription("Fetches the saved IMAP configuration from the database.");

        group.MapPost("/send", async ([FromBody] SendEmailRequest request, [FromServices] AppServices.Email.IEmailService service) =>
        {
            _ = await service.SendEmailAsync(request.Recipients, request.Subject, request.HtmlMessage, cc: null);
            return Results.Ok();
        })
        .WithName("SendEmail")
        .WithSummary("Send an email through the configured email provider")
        .WithDescription("Sends an email using the configured outbound email provider.");

        group.MapPost("/test", async ([FromServices] AppServices.Email.IEmailService service) =>
            await service.TestApiConnectionAsync()
                ? Results.Ok()
                : Results.Problem("Configured email provider unavailable", statusCode: 500))
            .WithName("TestEmail")
            .WithSummary("Tests connectivity to the configured email provider")
            .WithDescription("Tests the configured outbound email provider.");

        group.MapPost("/imap/test", async (
            [FromBody] ImapEmailSettings settings, // <-- 1. Add this to read the settings from the request body
            [FromServices] AppServices.Email.IImapEmailService imap,
            HttpContext ctx,
            ILoggerFactory loggerFactory) =>
        {
            if (!ctx.Request.Headers.ContainsKey("Authorization"))
            {
                loggerFactory.CreateLogger("ImapTest").LogWarning("Missing Authorization header");
            }

            // <-- 2. Pass the 'settings' object into the method call
            return await imap.TestConnectionAsync(settings, CancellationToken.None)
                ? Results.Ok()
                : Results.Problem("IMAP connection failed", statusCode: 500);
        })
        .RequireAuthorization("HelpdeskAdmin")
        .WithName("TestImapEmail")
        .WithSummary("Tests connectivity to the IMAP inbox")
        .WithDescription("Attempts to connect to the configured IMAP mailbox.");

        group.MapPost("/imap/save", async ([FromBody] ImapEmailSettings settings, [FromServices] HelpdeskDbContext db) =>
        {
            var existing = await db.ImapEmailSettings.FirstOrDefaultAsync();
            if (existing is null)
            {
                db.ImapEmailSettings.Add(settings);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(settings);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        })
        .RequireAuthorization("HelpdeskAdmin")
        .WithName("SaveImapSettings")
        .WithSummary("Persists IMAP settings")
        .WithDescription("Saves IMAP configuration to the database");
    }
}

public record SendEmailRequest(IEnumerable<string> Recipients, string Subject, string HtmlMessage);
