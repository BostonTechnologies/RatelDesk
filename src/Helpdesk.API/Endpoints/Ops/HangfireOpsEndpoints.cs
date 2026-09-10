using System.Security.Claims;
using Helpdesk.API.Background;
using Helpdesk.Shared.DTOs.Ops;

namespace Helpdesk.API.Endpoints.Ops;

public static class HangfireOpsEndpoints
{
    public static void MapHangfireOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var ops = app.MapGroup("/api/v1/ops/hangfire")
            .WithTags("Ops")
            .RequireAuthorization("HelpdeskAdmin");

        ops.MapGet("/settings", async (
            IHangfireRuntimeAdminService hangfireRuntimeAdminService,
            CancellationToken token) =>
        {
            var settings = await hangfireRuntimeAdminService.GetSettingsAsync(token);
            return Results.Ok(settings);
        });

        ops.MapPut("/settings", async (
            UpdateHangfireRuntimeSettingsDto dto,
            ClaimsPrincipal user,
            IHangfireRuntimeAdminService hangfireRuntimeAdminService,
            CancellationToken token) =>
        {
            var updatedBy = user.Identity?.Name
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("preferred_username");

            var settings = await hangfireRuntimeAdminService.UpdateSettingsAsync(dto, updatedBy, token);
            return Results.Ok(settings);
        });
    }
}
