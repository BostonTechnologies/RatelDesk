using System.Security.Claims;
using Helpdesk.Shared.DTOs.SupportNotifications;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.SupportNotifications;

public static class UserSupportNotificationPreferenceEndpoints
{
    public static void MapUserSupportNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var currentUserGroup = app.MapGroup("/api/v1/support/notification-preferences")
            .WithTags("Support Notification Preferences")
            .RequireAuthorization("NotificationAccess");

        currentUserGroup.MapGet("/", async (ClaimsPrincipal user, [FromServices] IRepository<UserSupportNotificationPreference> repo) =>
        {
            var userId = ResolveUserId(user);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var items = (await repo.GetAllAsync())
                .Where(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.EventType)
                .Select(ToDto)
                .ToList();
            return Results.Ok(items);
        });

        currentUserGroup.MapPut("/", async (
            ClaimsPrincipal user,
            [FromBody] UpsertUserSupportNotificationPreferenceDto dto,
            [FromServices] IRepository<UserSupportNotificationPreference> repo) =>
        {
            var userId = ResolveUserId(user);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            dto.UserId = userId;
            return await UpsertAsync(dto, repo);
        });

        var adminGroup = app.MapGroup("/api/v1/support/users/{userId}/notification-preferences")
            .WithTags("Support Notification Preferences")
            .RequireAuthorization("HelpdeskAdmin");

        adminGroup.MapGet("/", async (string userId, [FromServices] IRepository<UserSupportNotificationPreference> repo) =>
        {
            var items = (await repo.GetAllAsync())
                .Where(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.EventType)
                .Select(ToDto)
                .ToList();
            return Results.Ok(items);
        });

        adminGroup.MapPut("/", async (
            string userId,
            [FromBody] UpsertUserSupportNotificationPreferenceDto dto,
            [FromServices] IRepository<UserSupportNotificationPreference> repo) =>
        {
            dto.UserId = userId;
            return await UpsertAsync(dto, repo);
        });
    }

    private static async Task<IResult> UpsertAsync(
        UpsertUserSupportNotificationPreferenceDto dto,
        IRepository<UserSupportNotificationPreference> repo)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId))
        {
            return Results.BadRequest("UserId is required.");
        }

        if (dto.Channel != SupportNotificationChannel.Email)
        {
            return Results.BadRequest("Only email support notifications are supported in v1.");
        }

        var existing = (await repo.GetAllAsync()).FirstOrDefault(x =>
            string.Equals(x.UserId, dto.UserId, StringComparison.OrdinalIgnoreCase) &&
            x.EventType == dto.EventType &&
            x.Channel == dto.Channel);
        if (existing is null)
        {
            var created = await repo.CreateAsync(new UserSupportNotificationPreference
            {
                UserId = dto.UserId.Trim(),
                EventType = dto.EventType,
                Channel = dto.Channel,
                IsEnabled = dto.IsEnabled
            });
            return Results.Created($"/api/v1/support/users/{created.UserId}/notification-preferences/{created.Id}", ToDto(created));
        }

        existing.IsEnabled = dto.IsEnabled;
        existing.UpdatedUtc = DateTimeOffset.UtcNow;
        var updated = await repo.UpdateAsync(existing);
        return Results.Ok(ToDto(updated!));
    }

    private static string? ResolveUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.FindFirstValue("preferred_username")
        ?? user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue("email");

    private static UserSupportNotificationPreferenceDto ToDto(UserSupportNotificationPreference source) => new()
    {
        Id = source.Id,
        UserId = source.UserId,
        EventType = source.EventType,
        Channel = source.Channel,
        IsEnabled = source.IsEnabled,
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc
    };
}
