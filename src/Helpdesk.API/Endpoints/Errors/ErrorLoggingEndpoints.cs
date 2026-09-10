using Helpdesk.Shared.DTOs.Logging;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Helpdesk.API.Endpoints.Errors;

public static class ErrorLoggingEndpoints
{
    public static void MapErrorLoggingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/errors", async (
            [FromBody] LogEntryDto dto,
            ClaimsPrincipal user,
            [FromServices] IErrorLogRepository repo) =>
        {
            var entry = new ErrorLog
            {
                Message = dto.Message,
                StackTrace = dto.StackTrace,
                Page = dto.Page,
                User = dto.User ?? user.Identity?.Name,
                Timestamp = DateTime.UtcNow
            };
            await repo.SaveAsync(entry);
            return Results.NoContent();
        })
        .RequireAuthorization("HelpdeskAdmin")
        .WithName("LogClientError")
        .WithSummary("Client error logging")
        .WithDescription("Captures errors from the UI");
    }
}
