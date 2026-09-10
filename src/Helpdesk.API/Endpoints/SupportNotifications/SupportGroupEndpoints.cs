using Helpdesk.Shared.DTOs.SupportNotifications;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.SupportNotifications;

public static class SupportGroupEndpoints
{
    public static void MapSupportGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/groups")
            .WithTags("Support Groups")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async ([FromServices] IRepository<SupportGroup> repo, string? owningOrganizationId = null) =>
        {
            var items = (await repo.GetAllAsync())
                .Where(x => string.IsNullOrWhiteSpace(owningOrganizationId) || string.Equals(x.OwningOrganizationId, owningOrganizationId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.OwningOrganizationId)
                .ThenBy(x => x.Name)
                .Select(ToDto)
                .ToList();
            return Results.Ok(items);
        });

        group.MapGet("/{id}", async (string id, [FromServices] IRepository<SupportGroup> repo) =>
            await repo.GetAsync(id) is { } item ? Results.Ok(ToDto(item)) : Results.NotFound());

        group.MapPost("/", async ([FromBody] CreateSupportGroupDto dto, [FromServices] IRepository<SupportGroup> repo) =>
        {
            if (string.IsNullOrWhiteSpace(dto.OwningOrganizationId) || string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest("Owning organization and name are required.");
            }

            var created = await repo.CreateAsync(new SupportGroup
            {
                OwningOrganizationId = dto.OwningOrganizationId.Trim(),
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                IsEnabled = dto.IsEnabled
            });
            return Results.Created($"/api/v1/support/groups/{created.Id}", ToDto(created));
        });

        group.MapPut("/{id}", async (string id, [FromBody] UpdateSupportGroupDto dto, [FromServices] IRepository<SupportGroup> repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null)
            {
                return Results.NotFound();
            }

            existing.OwningOrganizationId = dto.OwningOrganizationId.Trim();
            existing.Name = dto.Name.Trim();
            existing.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            existing.IsEnabled = dto.IsEnabled;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
            var updated = await repo.UpdateAsync(existing);
            return Results.Ok(ToDto(updated!));
        });

        group.MapDelete("/{id}", async (string id, [FromServices] IRepository<SupportGroup> repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }

    private static SupportGroupDto ToDto(SupportGroup source) => new()
    {
        Id = source.Id,
        OwningOrganizationId = source.OwningOrganizationId,
        Name = source.Name,
        Description = source.Description,
        IsEnabled = source.IsEnabled,
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc
    };
}
