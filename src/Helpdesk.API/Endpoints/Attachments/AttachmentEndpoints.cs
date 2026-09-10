using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Attachment;
using Helpdesk.Shared.Models;
using Helpdesk.Application.Services.Tickets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IO;

namespace Helpdesk.API.Endpoints.Attachments;

public static class AttachmentEndpoints
{
    /// <summary>
    /// Configures the attachment-related API endpoints for the application.
    /// </summary>
    /// <remarks>This method defines two endpoint groups: <list type="bullet"> <item> <description>
    /// <c>/api/v1/attachments</c>: Handles operations for individual attachments, such as retrieving a file by its
    /// GUID. </description> </item> <item> <description> <c>/api/v1/tickets/{ticketId}/attachments</c>: Manages
    /// attachments associated with specific tickets, including listing and uploading files. </description> </item>
    /// </list> Each endpoint group is secured with appropriate authorization policies and supports specific operations:
    /// <list type="bullet"> <item> <description> The <c>/api/v1/attachments</c> group requires the "AttachmentRead"
    /// policy and provides a GET endpoint to retrieve an attachment by its ID. </description> </item> <item>
    /// <description> The <c>/api/v1/tickets/{ticketId}/attachments</c> group includes endpoints for listing attachments
    /// (GET) and uploading new attachments (POST).  The POST endpoint requires the "AttachmentWrite" policy and
    /// disables antiforgery protection. </description> </item> </list></remarks>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> used to define the application's routing.</param>
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        // --- NEW ENDPOINT GROUP TO SERVE FILES ---
        // This group handles fetching a single attachment by its GUID
        var individualAttachmentGroup = app.MapGroup("/api/v1/attachments")
            .WithTags("Attachments")
            .RequireAuthorization("AttachmentRead");

        individualAttachmentGroup.MapGet("/{id:guid}", async (Guid id, [FromServices] HelpdeskDbContext db, IWebHostEnvironment env) =>
        {
            Attachment? attachment = await db.Attachments.FindAsync(id);
            if (attachment == null)
            {
                return Results.NotFound("Attachment not found.");
            }

            // This path must match exactly where you saved the files during upload
            var filePath = Path.Combine(env.ContentRootPath, "wwwroot", "attachments", attachment.FilePath);

            if (!File.Exists(filePath))
            {
                // This is a server-side data integrity issue; log it
                Console.WriteLine($"Error: File not found at {filePath} for attachment ID {id}.");
                return Results.Problem("File not found on server.", statusCode: 404);
            }

            // This is the magic part: return the actual file stream.
            // The browser will handle downloading it or displaying it based on the ContentType.
            return Results.File(filePath, attachment.ContentType, attachment.FileName);
        });

        // Your existing endpoint group
        var ticketAttachmentGroup = app.MapGroup("/api/v1/tickets/{ticketId}/attachments")
            .WithTags("Attachments")
            .RequireAuthorization("AttachmentRead");

        ticketAttachmentGroup.MapGet("/", async ([FromRoute] string ticketId, [FromServices] HelpdeskDbContext db) =>
        {
            var attachments = await db.Attachments
                .Where(a => a.TicketId == ticketId)
                .Select(a => new AttachmentDto(a.Id, a.TicketId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt))
                .ToListAsync();
            return Results.Ok(attachments);
        });

        ticketAttachmentGroup.MapPost("/", async (
            [FromRoute] string ticketId,
            [FromServices] ITicketAttachmentService attachmentService,
            ClaimsPrincipal user,
            HttpRequest request,
            CancellationToken token) =>
        {
            var files = request.Form.Files;
            if (files.Count == 0)
            {
                return Results.BadRequest("No files were provided.");
            }

            var uploads = new List<AttachmentUpload>();
            foreach (var file in files)
            {
                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms, token);
                uploads.Add(new AttachmentUpload(file.FileName, file.ContentType, ms.ToArray()));
            }

            var uploadedById = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var saved = await attachmentService.SaveAsync(ticketId, uploads, uploadedById, token);
            return Results.Ok(saved);
        }).RequireAuthorization("AttachmentWrite")
          .DisableAntiforgery();
    }
}