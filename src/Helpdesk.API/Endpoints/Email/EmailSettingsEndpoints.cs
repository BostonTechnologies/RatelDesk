using AppServices = Helpdesk.Application.Services;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Endpoints.Email;

public static class EmailSettingsEndpoints
{
    public static void MapEmailSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        MapEmailSettingsGroup(app.MapGroup("/api/v1/email-settings"));
        MapEmailSettingsGroup(app.MapGroup("/api/email-settings").ExcludeFromDescription());
    }

    private static void MapEmailSettingsGroup(RouteGroupBuilder group)
    {
        group
            .RequireAuthorization("HelpdeskAdmin")
            .WithTags("Email Settings");

        group.MapGet("/", async ([FromServices] HelpdeskDbContext db, CancellationToken ct) =>
        {
            var settings = await AppServices.Email.ImapEmailService.LoadOrderedInboxSettingsAsync(
                db.EmailInboxSettings.AsNoTracking(), ct);
            var result = settings.Select(ToDto);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async ([FromServices] HelpdeskDbContext db, Guid id, CancellationToken ct) =>
        {
            var s = await db.EmailInboxSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) return Results.NotFound();
            return Results.Ok(ToDto(s));
        });

        group.MapPost("/", async ([FromServices] HelpdeskDbContext db, [FromBody] EmailInboxSettings dto, CancellationToken ct) =>
        {
            var existing = dto.Id == Guid.Empty
                ? (await AppServices.Email.ImapEmailService.LoadOrderedInboxSettingsAsync(db.EmailInboxSettings, ct)).FirstOrDefault()
                : await db.EmailInboxSettings.FirstOrDefaultAsync(e => e.Id == dto.Id, ct);
            if (existing is null)
            {
                dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
                Normalize(dto);
                dto.CreatedAt = dto.UpdatedAt = DateTimeOffset.UtcNow;
                db.EmailInboxSettings.Add(dto);
            }
            else
            {
                ApplyUpdate(existing, dto);
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(existing ?? dto));
        });

        group.MapPut("/{id:guid}", async ([FromServices] HelpdeskDbContext db, Guid id, [FromBody] EmailInboxSettings dto, CancellationToken ct) =>
        {
            var existing = await db.EmailInboxSettings.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (existing is null) return Results.NotFound();
            ApplyUpdate(existing, dto);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(existing));
        });

        group.MapPost("/test", async (
            [FromServices] HelpdeskDbContext db,
            [FromServices] AppServices.Email.IImapEmailService imap,
            [FromBody] EmailInboxSettings dto,
            CancellationToken ct) =>
        {
            var secret = dto.ClientSecret;
            if (string.IsNullOrWhiteSpace(secret) && dto.Id != Guid.Empty)
            {
                var existing = await db.EmailInboxSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == dto.Id, ct);
                secret = existing?.ClientSecret;
            }

            var testSettings = Helpdesk.Application.Services.Email.ImapEmailService.ToImapSettings(dto, requireBackgroundSync: false);
            testSettings.ClientSecret = secret ?? string.Empty;

            return await imap.TestConnectionAsync(testSettings, ct)
                ? Results.Ok(new { success = true, message = "Connection successful." })
                : Results.Problem("IMAP connection failed", statusCode: 500);
        });

        group.MapPost("/{id:guid}/enable", async ([FromServices] HelpdeskDbContext db, Guid id, CancellationToken ct) =>
        {
            var s = await db.EmailInboxSettings.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (s is null) return Results.NotFound();
            s.Enabled = true;
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(s);
        });

        group.MapPost("/{id:guid}/disable", async ([FromServices] HelpdeskDbContext db, Guid id, CancellationToken ct) =>
        {
            var s = await db.EmailInboxSettings.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (s is null) return Results.NotFound();
            s.Enabled = false;
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(s);
        });

        group.MapPost("/{id:guid}/test", async (
            [FromServices] HelpdeskDbContext db,
            [FromServices] AppServices.Email.IImapEmailService imap,
            Guid id,
            CancellationToken ct) =>
        {
            var settings = await db.EmailInboxSettings.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            if (settings is null) return Results.NotFound();
            var testSettings = Helpdesk.Application.Services.Email.ImapEmailService.ToImapSettings(settings, requireBackgroundSync: false);
            return await imap.TestConnectionAsync(testSettings, ct)
                ? Results.Ok(new { success = true, message = "Connection successful." })
                : Results.Problem("IMAP connection failed", statusCode: 500);
        });
    }

    private static EmailInboxSettingsDto ToDto(EmailInboxSettings s)
        => new(
            s.Id,
            s.MailHost,
            s.Port,
            s.UseSsl,
            s.MailboxAddress,
            s.TenantId,
            s.ClientId,
            s.MailboxFolder,
            s.Enabled,
            s.BackgroundSyncEnabled,
            !string.IsNullOrEmpty(s.ClientSecret));

    private static void ApplyUpdate(EmailInboxSettings existing, EmailInboxSettings incoming)
    {
        var preservedSecret = existing.ClientSecret;
        existing.MailHost = incoming.MailHost;
        existing.Port = incoming.Port;
        existing.UseSsl = incoming.UseSsl;
        existing.MailboxAddress = incoming.MailboxAddress;
        existing.TenantId = incoming.TenantId;
        existing.ClientId = incoming.ClientId;
        existing.ClientSecret = string.IsNullOrWhiteSpace(incoming.ClientSecret)
            ? preservedSecret
            : incoming.ClientSecret;
        existing.MailboxFolder = incoming.MailboxFolder;
        existing.Enabled = incoming.Enabled;
        existing.BackgroundSyncEnabled = incoming.BackgroundSyncEnabled;
        Normalize(existing);
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Normalize(EmailInboxSettings settings)
    {
        settings.MailHost = string.IsNullOrWhiteSpace(settings.MailHost)
            ? "outlook.office365.com"
            : settings.MailHost.Trim();
        settings.MailboxAddress = settings.MailboxAddress?.Trim() ?? string.Empty;
        settings.TenantId = settings.TenantId?.Trim() ?? string.Empty;
        settings.ClientId = settings.ClientId?.Trim() ?? string.Empty;
        settings.MailboxFolder = string.IsNullOrWhiteSpace(settings.MailboxFolder)
            ? "INBOX"
            : settings.MailboxFolder.Trim();
        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            settings.ClientSecret = settings.ClientSecret.Trim();
        }
    }
}
