using System.Security.Claims;
using System.Text.Json;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Timeline;
using Helpdesk.Application.WorkLogs;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Storage;
using Helpdesk.Shared.DTOs.Worklog;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.API.Endpoints.WorkLogs;

public static class WorkLogEndpoints
{
    public static void MapWorkLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/incidents")
            .WithTags("Work Logs")
            .RequireAuthorization();

        group.MapGet("/{id}/timeline", async (
            [FromRoute] string id,
            [FromQuery] string? order,
            [FromServices] HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var timelineQuery = db.TicketTimelineEvents
                .AsNoTracking()
                .Where(evt => evt.TicketId == id);

            timelineQuery = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase)
                ? timelineQuery.OrderBy(evt => evt.CreatedUtc)
                : timelineQuery.OrderByDescending(evt => evt.CreatedUtc);

            var timeline = await timelineQuery
                .Select(evt => ToDto(evt))
                .ToListAsync(ct);

            return Results.Ok(timeline);
        })
        .WithName("GetIncidentTimeline")
        .WithSummary("Gets timeline events for an incident")
        .WithDescription("Retrieves timeline events for the specified incident ordered by CreatedUtc. Defaults to descending (newest first); use ?order=asc for oldest first.");

        group.MapGet("/{id}/timeline/stream", StreamTimeline)
            .WithName("StreamIncidentTimeline")
            .WithSummary("Streams incident timeline events in real time")
            .WithDescription("Pushes timeline updates over server-sent events (SSE).");

        group.MapPost("/{id}/worklogs", async (
            [FromRoute] string id,
            [FromBody] CreateWorkLogDto dto,
            ClaimsPrincipal user,
            [FromServices] IRequestSender sender) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Notes))
            {
                return Results.Problem("Notes cannot be empty", statusCode: 400);
            }

            var techId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var techName = user.Identity?.Name;
            var created = await sender.Send(new CreateWorkLogCommand(
                id,
                dto.Hours,
                dto.Notes,
                techId,
                techName,
                NotifyCustomer: !dto.IsInternalNote,
                IsInternalNote: dto.IsInternalNote));

            var response = new WorkLogDto
            {
                Id = created.Id,
                TicketId = created.TicketId,
                NotesHtml = created.NotesHtml,
                NotesText = created.NotesText,
                Hours = created.Hours,
                IsInternalNote = created.IsInternalNote,
                LoggedAt = created.LoggedAt,
                TechnicianId = created.TechnicianId,
                TechnicianName = techName
            };

            return Results.Created($"/api/v1/incidents/{id}/worklogs/{response.Id}", response);
        })
        .WithName("CreateIncidentWorkLog")
        .WithSummary("Creates a work log for an incident")
        .WithDescription("Adds a work log to the incident and notifies the customer");

        app.MapGet("/api/worklogs/{worklogId}/images/{filename}", (
            [FromRoute] string worklogId,
            [FromRoute] string filename,
            IWebHostEnvironment env,
            HttpContext httpContext,
            IImageLinkSigner signer,
            ILoggerFactory loggerFactory,
            IOptions<StorageOptions> storageOptions) =>
        {
            var logger = loggerFactory.CreateLogger("WorklogInlineImage");
            var safeWorklogId = SanitizePathSegment(worklogId);
            var safeFilename = Path.GetFileName(filename);
            if (!string.Equals(filename, safeFilename, StringComparison.Ordinal))
                return Results.BadRequest("Invalid file name.");

            var token = httpContext.Request.Query["token"].ToString();
            if (!signer.ValidateToken(token, "worklog", safeWorklogId, safeFilename))
            {
                logger.LogWarning("Worklog inline image token denied. WorklogId={WorklogId} File={File}", safeWorklogId, safeFilename);
                return Results.Unauthorized();
            }

            var storageRoot = string.IsNullOrWhiteSpace(storageOptions.Value.RootPath)
                ? Path.Combine(env.ContentRootPath, "storage")
                : storageOptions.Value.RootPath;
            var rootPath = Path.Combine(storageRoot, "worklogs");
            var fullPath = Path.GetFullPath(Path.Combine(rootPath, safeWorklogId, "inline", safeFilename));
            var fullRoot = Path.GetFullPath(rootPath);

            if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
                return Results.BadRequest("Invalid path.");

            if (!File.Exists(fullPath))
                return Results.NotFound();

            logger.LogInformation("Serving worklog inline image. WorklogId={WorklogId} Path={Path}", safeWorklogId, fullPath);

            var contentType = ContentTypeHelper.GetContentType(safeFilename);
            return Results.File(fullPath, contentType);
        })
        .WithTags("Work Logs")
        .WithName("GetWorklogImage")
        .WithSummary("Gets a stored worklog inline image");
    }

    private static async Task StreamTimeline(
        [FromRoute] string id,
        HttpContext context,
        [FromServices] ITimelineEventBus eventBus,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("WorkLogEndpoints");
        var reader = eventBus.Subscribe(id);

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        context.Response.ContentType = "text/event-stream";

        try
        {
            logger.LogInformation("Timeline stream connected {TicketId}", id);
            await context.Response.StartAsync(ct);
            await context.Response.WriteAsync(": connected\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            var keepAliveInterval = TimeSpan.FromSeconds(15);

            while (!ct.IsCancellationRequested)
            {
                var waitForDataTask = reader.WaitToReadAsync(ct).AsTask();
                var keepAliveTask = Task.Delay(keepAliveInterval, ct);
                var completedTask = await Task.WhenAny(waitForDataTask, keepAliveTask);

                if (completedTask == waitForDataTask)
                {
                    if (!await waitForDataTask)
                    {
                        break;
                    }

                    while (reader.TryRead(out var evt))
                    {
                        var json = JsonSerializer.Serialize(evt);

                        await context.Response.WriteAsync("event: timeline\n", ct);
                        await context.Response.WriteAsync($"data: {json}\n\n", ct);
                    }

                    await context.Response.Body.FlushAsync(ct);
                }
                else
                {
                    await context.Response.WriteAsync(": keepalive\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // connection terminated by client disconnect/cancellation
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Timeline stream error {TicketId}", id);
        }
        finally
        {
            logger.LogInformation("Timeline stream disconnected {TicketId}", id);
            eventBus.Unsubscribe(id, reader);
        }
    }

    private static TicketTimelineEventDto ToDto(TicketTimelineEvent evt)
    {
        return new TicketTimelineEventDto
        {
            Id = evt.Id,
            TicketId = evt.TicketId,
            CreatedUtc = evt.CreatedUtc,
            CreatedByUserId = evt.CreatedByUserId,
            CreatedByUserName = evt.CreatedByUserName,
            EventType = evt.EventType,
            MessageHtml = evt.MessageHtml,
            MessageText = evt.MessageText,
            EmailStatus = evt.EmailStatus,
            EmailRecipient = evt.EmailRecipient,
            RetryCount = evt.RetryCount,
            IsRetryable = evt.IsRetryable
        };
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var clean = global::System.Text.RegularExpressions.Regex.Replace(value, "[^a-zA-Z0-9_-]", "-");
        return string.IsNullOrWhiteSpace(clean) ? "unknown" : clean;
    }
}
