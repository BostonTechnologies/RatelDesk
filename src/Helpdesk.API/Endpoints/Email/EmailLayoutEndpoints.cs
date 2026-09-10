using Helpdesk.Application.WorkLogs;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.Email;

public static class EmailLayoutEndpoints
{
    public static void MapEmailLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/email-layouts")
            .WithTags("Email Layouts")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async (
            [FromQuery] int? tenantId,
            [FromServices] IRepository<EmailLayout> repo) =>
        {
            var all = await repo.GetAllAsync();
            var items = tenantId.HasValue
                ? all.Where(x => x.TenantId == tenantId.Value || x.TenantId is null)
                : all;

            return Results.Ok(items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        });

        group.MapGet("/choices", async (
            [FromServices] IRepository<EmailLayout> repo,
            [FromServices] ITenantContext tenantContext) =>
        {
            var all = await repo.GetAllAsync();
            var currentTenantId = ParseTenantId(tenantContext.TenantId);

            var choices = all
                .Where(x => x.IsSystem || (currentTenantId.HasValue && x.TenantId == currentTenantId))
                .OrderBy(x => x.IsSystem ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new EmailLayoutChoiceDto(x.Id, x.Name))
                .ToList();

            return Results.Ok(choices);
        });

        group.MapPost("/", async (
            [FromBody] EmailLayout layout,
            [FromServices] IRepository<EmailLayout> repo,
            [FromServices] IHtmlSanitizerService sanitizer) =>
        {
            layout.HtmlContent = sanitizer.Sanitize(layout.HtmlContent ?? string.Empty);
            layout.CreatedUtc = DateTime.UtcNow;
            layout.UpdatedUtc = null;
            if (layout.Version < 1)
                layout.Version = 1;

            await repo.CreateAsync(layout);
            return Results.Created($"/api/v1/email-layouts/{layout.Id}", layout);
        });

        group.MapPut("/{id:int}", async (
            int id,
            [FromBody] EmailLayout layout,
            [FromServices] IRepository<EmailLayout> repo,
            [FromServices] IHtmlSanitizerService sanitizer) =>
        {
            var existing = await repo.GetAsync(id.ToString());
            if (existing is null)
                return Results.NotFound();

            existing.Name = layout.Name;
            existing.HtmlContent = sanitizer.Sanitize(layout.HtmlContent ?? string.Empty);
            existing.TenantId = layout.TenantId;
            existing.UpdatedUtc = DateTime.UtcNow;
            existing.Version = Math.Max(existing.Version + 1, 1);

            await repo.UpdateAsync(existing);
            return Results.Ok(existing);
        });

        group.MapDelete("/{id:int}", async (int id, [FromServices] IRepository<EmailLayout> repo) =>
        {
            var existing = await repo.GetAsync(id.ToString());
            if (existing is null)
                return Results.NotFound();
            if (existing.IsSystem)
                return Results.BadRequest("System layouts cannot be deleted.");

            await repo.DeleteAsync(id.ToString());
            return Results.NoContent();
        });
    }

    private static int? ParseTenantId(string? value)
    {
        if (int.TryParse(value, out var tenantId))
            return tenantId;

        return null;
    }

    private sealed record EmailLayoutChoiceDto(int Id, string Name);
}
