using Helpdesk.Shared.DTOs.SupportNotifications;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.SupportNotifications;

public static class SupportGroupMemberEndpoints
{
    public static void MapSupportGroupMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/groups/{groupId}/members")
            .WithTags("Support Group Members")
            .RequireAuthorization("HelpdeskAdmin");

        group.MapGet("/", async (string groupId, [FromServices] IRepository<SupportGroupMember> repo, [FromServices] IRepository<User> users) =>
        {
            var userLookup = (await users.GetAllAsync()).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var items = (await repo.GetAllAsync())
                .Where(x => string.Equals(x.SupportGroupId, groupId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.UserId)
                .Select(x => ToDto(x, userLookup))
                .ToList();
            return Results.Ok(items);
        });

        group.MapPost("/", async (string groupId, [FromBody] CreateSupportGroupMemberDto dto, [FromServices] IRepository<SupportGroupMember> repo) =>
        {
            if (string.IsNullOrWhiteSpace(dto.UserId))
            {
                return Results.BadRequest("UserId is required.");
            }

            var created = await repo.CreateAsync(new SupportGroupMember
            {
                SupportGroupId = groupId,
                UserId = dto.UserId.Trim(),
                Role = dto.Role,
                Source = dto.Source,
                IsEnabled = dto.IsEnabled
            });
            return Results.Created($"/api/v1/support/groups/{groupId}/members/{created.Id}", ToDto(created, new Dictionary<string, User>()));
        });

        group.MapPut("/{id}", async (string groupId, string id, [FromBody] UpdateSupportGroupMemberDto dto, [FromServices] IRepository<SupportGroupMember> repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null || !string.Equals(existing.SupportGroupId, groupId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            existing.Role = dto.Role;
            existing.Source = dto.Source;
            existing.IsEnabled = dto.IsEnabled;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
            var updated = await repo.UpdateAsync(existing);
            return Results.Ok(ToDto(updated!, new Dictionary<string, User>()));
        });

        group.MapDelete("/{id}", async (string groupId, string id, [FromServices] IRepository<SupportGroupMember> repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null || !string.Equals(existing.SupportGroupId, groupId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            return await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound();
        });
    }

    private static SupportGroupMemberDto ToDto(SupportGroupMember source, IReadOnlyDictionary<string, User> users)
    {
        users.TryGetValue(source.UserId, out var user);
        return new SupportGroupMemberDto
        {
            Id = source.Id,
            SupportGroupId = source.SupportGroupId,
            UserId = source.UserId,
            UserName = user?.Name,
            UserEmail = user?.Email,
            Role = source.Role,
            Source = source.Source,
            IsEnabled = source.IsEnabled,
            CreatedUtc = source.CreatedUtc,
            UpdatedUtc = source.UpdatedUtc
        };
    }
}
