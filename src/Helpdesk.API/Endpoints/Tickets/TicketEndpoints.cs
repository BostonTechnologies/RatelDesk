using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Helpdesk.API.Background;
using Helpdesk.Application.Messaging;
using Helpdesk.Application.Orchestration;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Application.Sla;
using Helpdesk.Application.Tickets;
using Helpdesk.Application.Workflow;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.DTOs.RequestForm;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Services.KB;
using Helpdesk.Application.Services.AI;
using Helpdesk.Application.WorkLogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using TicketRequest = Helpdesk.Shared.Models.Request;

namespace Helpdesk.API.Endpoints.Tickets;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tickets")
            .WithTags("Tickets")
            .RequireAuthorization();

        var ticketCountGroup = app.MapGroup("/api/v1/{ticketType}/{ticketId}")
            .WithTags("Tickets")
            .RequireAuthorization();

        ticketCountGroup.MapGet("/timeline/count", async (
            [FromRoute] string ticketType,
            [FromRoute] string ticketId,
            HttpContext context,
            [FromServices] ICurrentUserAccessService accessService,
            [FromServices] HelpdeskDbContext dbContext,
            CancellationToken ct) =>
        {
            var authorizationFailure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, accessService, dbContext, ct);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var count = await dbContext.TicketTimelineEvents
                .AsNoTracking()
                .Where(x => x.TicketId == ticketId)
                .CountAsync(ct);

            return Results.Ok(count);
        });

        ticketCountGroup.MapGet("/attachments/count", async (
            [FromRoute] string ticketType,
            [FromRoute] string ticketId,
            HttpContext context,
            [FromServices] ICurrentUserAccessService accessService,
            [FromServices] HelpdeskDbContext dbContext,
            CancellationToken ct) =>
        {
            var authorizationFailure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, accessService, dbContext, ct);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var count = await dbContext.Attachments
                .AsNoTracking()
                .Where(x => x.TicketId == ticketId)
                .CountAsync(ct);

            return Results.Ok(count);
        });

        ticketCountGroup.MapGet("/listeners/count", async (
            [FromRoute] string ticketType,
            [FromRoute] string ticketId,
            HttpContext context,
            [FromServices] ICurrentUserAccessService accessService,
            [FromServices] HelpdeskDbContext dbContext,
            CancellationToken ct) =>
        {
            var authorizationFailure = await AuthorizeTicketViewAsync(ticketType, ticketId, context.User, accessService, dbContext, ct);
            if (authorizationFailure is not null)
            {
                return authorizationFailure;
            }

            var count = ticketType.ToLowerInvariant() switch
            {
                "incidents" => await dbContext.Incidents
                    .AsNoTracking()
                    .Where(x => x.Id == ticketId)
                    .Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count)
                    .SingleOrDefaultAsync(ct),
                "requests" => await dbContext.Requests
                    .AsNoTracking()
                    .Where(x => x.Id == ticketId)
                    .Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count)
                    .SingleOrDefaultAsync(ct),
                "changes" => await dbContext.Changes
                    .AsNoTracking()
                    .Where(x => x.Id == ticketId)
                    .Select(x => (x.RequesterEmail != null ? 1 : 0) + x.CcRecipients.Count)
                    .SingleOrDefaultAsync(ct),
                _ => 0
            };

            return Results.Ok(count);
        });

        MapTicketCustomerEndpoint(ticketCountGroup);

        group.MapPost("/{ticketId}/mark-as-seen", async ([FromRoute] string ticketId, [FromServices] IRequestSender sender, ClaimsPrincipal user) =>
            {
                await sender.Send(new MarkTicketAsSeenCommand(ticketId));
                return Results.NoContent();
            })
        .WithName("MarkTicketAsSeen")
        .WithSummary("Marks a ticket as viewed")
        .WithDescription("Sets LastViewedByCustomerAt and worklog read receipts.");

        group.MapPost("/{id:guid}/suggest-knowledge", async (
            Guid id,
            IAiSuggestionQueue queue,
            HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var exists = await db.Tickets.AsNoTracking().AnyAsync(t => t.Id == id.ToString(), ct);
            if (!exists) return Results.NotFound();

            var orgId = await db.Tickets.Where(t => t.Id == id.ToString()).Select(t => t.OrganizationId).FirstAsync(ct);
            var org = await db.OrganizationAiKbSettings.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.OrganizationId == orgId, ct);

            if (org is null || org.EnableAiSearch == false)
                return Results.StatusCode(StatusCodes.Status204NoContent);

            await queue.QueueAsync(id.ToString(), ct);
            return Results.Accepted($"/api/v1/tickets/{id}/suggest-knowledge");
        })
        .WithSummary("Queue knowledge suggestions for a ticket")
        .WithDescription("Non-blocking. Returns 202 and processes in background.");

        group.MapGet("/{id:guid}/suggest-knowledge", async (
            Guid id,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var authorizationFailure = await AuthorizeTicketViewAsync("incidents", id.ToString(), context.User, accessService, db, ct);
            if (authorizationFailure is not null) return authorizationFailure;
            var row = await db.TicketAiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.TicketId == id.ToString(), ct);
            if (row is null) return Results.NoContent();

            var items = JsonSerializer.Deserialize<List<KnowledgeSuggestion>>(row.ItemsJson) ?? new();
            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}/ai-feedback", async (
            Guid id,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var ticketId = id.ToString();
            var authorizationFailure = await AuthorizeTicketViewAsync("incidents", ticketId, context.User, accessService, db, ct);
            if (authorizationFailure is not null) return authorizationFailure;

            var items = await db.TicketAiFeedback
                .AsNoTracking()
                .Where(x => x.TicketId == ticketId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TicketAiFeedbackDto
                {
                    Id = x.Id,
                    FeedbackType = x.FeedbackType,
                    FeedbackValue = x.FeedbackValue,
                    ArticleId = x.ArticleId,
                    RequestId = x.RequestId,
                    Notes = x.Notes,
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedByName = x.CreatedByName,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithSummary("List AI feedback for a ticket")
        .WithDescription("Returns operator feedback entries for AI suggestions and automation outcomes on the ticket.");

        group.MapGet("/{id:guid}/ai-audit", async (
            Guid id,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var ticketId = id.ToString();
            var authorizationFailure = await AuthorizeTicketViewAsync("incidents", ticketId, context.User, accessService, db, ct);
            if (authorizationFailure is not null) return authorizationFailure;

            var items = await db.AiOperationAuditRecords
                .AsNoTracking()
                .Where(x => x.SubjectId == ticketId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TicketAiAuditEntryDto
                {
                    Id = x.Id,
                    OperationName = x.OperationName,
                    OrganizationId = x.OrganizationId,
                    ProviderName = x.ProviderName,
                    ModelId = x.ModelId,
                    CorrelationId = x.CorrelationId,
                    SubjectId = x.SubjectId,
                    Notes = x.Notes,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithSummary("List AI audit activity for a ticket")
        .WithDescription("Returns persisted AI operation audit records for the ticket, including generation, approval, and automation actions.");

        group.MapPost("/{id:guid}/ai-feedback", async (
            Guid id,
            [FromBody] SubmitTicketAiFeedbackDto dto,
            HelpdeskDbContext db,
            IRequestSender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var ticketId = id.ToString();
            var exists = await db.Tickets.AsNoTracking().AnyAsync(x => x.Id == ticketId, ct);
            if (!exists)
            {
                return Results.NotFound();
            }

            var feedbackType = dto.FeedbackType?.Trim().ToLowerInvariant();
            var feedbackValue = dto.FeedbackValue?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(feedbackType) || string.IsNullOrWhiteSpace(feedbackValue))
            {
                return Results.BadRequest("FeedbackType and FeedbackValue are required.");
            }

            if (feedbackType is not ("suggestion" or "automation"))
            {
                return Results.BadRequest("FeedbackType must be 'suggestion' or 'automation'.");
            }

            if (feedbackType == "suggestion" && string.IsNullOrWhiteSpace(dto.ArticleId))
            {
                return Results.BadRequest("ArticleId is required for suggestion feedback.");
            }

            if (feedbackType == "automation" && string.IsNullOrWhiteSpace(dto.RequestId))
            {
                return Results.BadRequest("RequestId is required for automation feedback.");
            }

            var entry = new TicketAiFeedback
            {
                TicketId = ticketId,
                FeedbackType = feedbackType,
                FeedbackValue = feedbackValue,
                ArticleId = string.IsNullOrWhiteSpace(dto.ArticleId) ? null : dto.ArticleId.Trim(),
                RequestId = string.IsNullOrWhiteSpace(dto.RequestId) ? null : dto.RequestId.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedByName = user.Identity?.Name
            };

            db.TicketAiFeedback.Add(entry);
            await db.SaveChangesAsync(ct);

            var techId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var techName = user.Identity?.Name;

            string feedbackNote;
            if (feedbackType == "suggestion")
            {
                var articleTitle = string.IsNullOrWhiteSpace(entry.ArticleId)
                    ? null
                    : await db.KnowledgeBaseArticles
                        .AsNoTracking()
                        .Where(x => x.Id.ToString() == entry.ArticleId)
                        .Select(x => x.Title)
                        .FirstOrDefaultAsync(ct);

                feedbackNote = string.IsNullOrWhiteSpace(articleTitle)
                    ? $"Recorded AI suggestion feedback: {feedbackValue.Replace('_', ' ')}."
                    : $"Recorded AI suggestion feedback: {feedbackValue.Replace('_', ' ')} for KB article '{articleTitle}'.";
            }
            else
            {
                var requestTrackingId = string.IsNullOrWhiteSpace(entry.RequestId)
                    ? null
                    : await db.Requests
                        .AsNoTracking()
                        .Where(x => x.Id == entry.RequestId)
                        .Select(x => x.TrackingId)
                        .FirstOrDefaultAsync(ct);

                feedbackNote = string.IsNullOrWhiteSpace(requestTrackingId)
                    ? $"Recorded AI automation outcome: {feedbackValue.Replace('_', ' ')}."
                    : $"Recorded AI automation outcome: {feedbackValue.Replace('_', ' ')} for request {requestTrackingId}.";
            }

            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                feedbackNote = $"{feedbackNote} Notes: {entry.Notes}";
            }

            await sender.Send(
                new CreateWorkLogCommand(
                    ticketId,
                    0,
                    feedbackNote,
                    techId,
                    techName,
                    NotifyCustomer: false),
                ct);

            return Results.Ok(new TicketAiFeedbackDto
            {
                Id = entry.Id,
                FeedbackType = entry.FeedbackType,
                FeedbackValue = entry.FeedbackValue,
                ArticleId = entry.ArticleId,
                RequestId = entry.RequestId,
                Notes = entry.Notes,
                CreatedByUserId = entry.CreatedByUserId,
                CreatedByName = entry.CreatedByName,
                CreatedAt = entry.CreatedAt
            });
        })
        .WithSummary("Submit AI feedback for a ticket")
        .WithDescription("Persists operator feedback for AI KB suggestions or automation outcomes on the ticket.");

        group.MapGet("/{id:guid}/requester-reply-draft", async (
            Guid id,
            IRepository<Ticket> tickets,
            IRequesterReplyDraftService requesterReplyDraftService,
            CancellationToken ct) =>
        {
            var ticket = await tickets.GetAsync(id.ToString());
            if (ticket is null)
            {
                return Results.NotFound();
            }

            var draft = await requesterReplyDraftService.GenerateAsync(ticket, ct);
            return draft is null ? Results.NoContent() : Results.Ok(draft);
        })
        .WithSummary("Generate requester-facing reply draft for a ticket")
        .WithDescription("Returns a requester-safe reply or clarification draft grounded in KB retrieval results.");

        group.MapPost("/{id:guid}/requester-reply-draft/approve-send", async (
            Guid id,
            [FromBody] ApproveRequesterReplyDraftDto dto,
            HelpdeskDbContext db,
            IRepository<Ticket> tickets,
            IRequesterReplyDraftService requesterReplyDraftService,
            IAiOperationAuditService aiAudit,
            IRequestSender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var ticket = await tickets.GetAsync(id.ToString());
            if (ticket is null)
            {
                return Results.NotFound();
            }

            var incidentExists = await db.Incidents.AsNoTracking().AnyAsync(x => x.Id == ticket.Id, ct);
            if (!incidentExists)
            {
                return Results.Problem("Requester draft send is currently supported for incidents only.", statusCode: 400);
            }

            if (string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                return Results.Problem("Ticket has no requester email.", statusCode: 400);
            }

            var draft = await requesterReplyDraftService.GenerateAsync(ticket, ct);
            if (draft is null || string.IsNullOrWhiteSpace(draft.DraftReply))
            {
                return Results.Problem("No requester draft is available for this ticket.", statusCode: 400);
            }

            var replyBody = string.IsNullOrWhiteSpace(dto.EditedReply)
                ? draft.DraftReply
                : dto.EditedReply.Trim();

            if (dto.IncludeFollowUpQuestions && draft.FollowUpQuestions.Count > 0)
            {
                replyBody = string.Join(
                    Environment.NewLine,
                    [
                        replyBody,
                        string.Empty,
                        "Please confirm the following:",
                        ..draft.FollowUpQuestions.Select(question => $"- {question}")
                    ]);
            }

            var techId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var techName = user.Identity?.Name;
            var created = await sender.Send(new CreateWorkLogCommand(ticket.Id, 0, replyBody, techId, techName), ct);
            var requesterDraftNote = string.IsNullOrWhiteSpace(draft.SuggestedArticleTitle)
                ? $"Approved AI requester draft ({draft.ConfidenceLabel})."
                : $"Approved AI requester draft grounded on KB article '{draft.SuggestedArticleTitle}' ({draft.ConfidenceLabel}).";
            await sender.Send(
                new CreateWorkLogCommand(
                    ticket.Id,
                    0,
                    requesterDraftNote,
                    techId,
                    techName,
                    NotifyCustomer: false),
                ct);

            await aiAudit.RecordAsync(
                new AiOperationAuditEntry(
                    "ticket-requester-reply-approved",
                    ticket.OrganizationId,
                    "human-approved",
                    draft.ConfidenceLabel,
                    SubjectId: ticket.Id,
                    Notes: draft.RequiresClarification ? "clarification-draft" : "reply-draft"),
                ct);

            return Results.Ok(new
            {
                WorklogId = created.Id,
                SentTo = ticket.RequesterEmail,
                draft.RequiresClarification
            });
        })
        .WithSummary("Approve and send requester-facing reply draft")
        .WithDescription("Sends the current requester draft through the standard incident worklog/email notification path.");

        group.MapPost("/{id:guid}/automation-approvals", async (
            Guid id,
            [FromBody] ApproveTicketAutomationDto dto,
            HelpdeskDbContext db,
            IRepository<Ticket> tickets,
            IRepository<Request> requestRepo,
            IRepository<RequestTask> requestTaskRepo,
            IRequestFormSchemaParser requestFormSchemaParser,
            IAutomationBindingPayloadContractService automationPayloadContractService,
            ITicketSlaInitializer ticketSlaInitializer,
            IRequestTaskGenerationService requestTaskGenerationService,
            IWorkflowEngine workflowEngine,
            IAiOperationAuditService aiAudit,
            IRequestSender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var ticket = await tickets.GetAsync(id.ToString());
            if (ticket is not Incident incident)
            {
                return Results.BadRequest("Automation approval is currently supported for incidents only.");
            }

            var validation = await ValidateTicketAutomationApprovalAsync(
                db,
                requestFormSchemaParser,
                automationPayloadContractService,
                incident,
                dto.ArticleId,
                ct);

            if (!validation.Success)
            {
                return validation.ToHttpResult();
            }

            var article = validation.Article!;
            var requestForm = validation.RequestForm!;

            Request created;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                created = await requestRepo.CreateAsync(new Request
                {
                    Title = $"Automation: {article.Title}",
                    Description = incident.Description,
                    Priority = incident.Priority,
                    CustomerId = incident.CustomerId,
                    OrganizationId = incident.OrganizationId,
                    DueDate = incident.DueDate,
                    ServiceId = incident.ServiceId ?? requestForm.ServiceId,
                    RequestFormId = requestForm.Id,
                    PayloadJson = validation.PayloadJson!,
                    SourceTicketId = incident.Id,
                    SourceKnowledgeArticleId = article.Id,
                    SourceAutomationBindingId = article.AutomationBindingId,
                    LinkedAssetIds = [.. incident.LinkedAssetIds],
                    Attachments = [.. incident.Attachments],
                    RequesterEmail = incident.RequesterEmail
                });

                try
                {
                    await ticketSlaInitializer.InitializeAsync(created);
                }
                catch
                {
                }

                await requestTaskGenerationService.GenerateForRequestAsync(created, ct);
                await tx.CommitAsync(ct);
            }

            await workflowEngine.RunAsync(created.Id, WorkflowRunReason.RequestCreated, ct);

            var createdTask = await requestTaskRepo.Query()
                .AsNoTracking()
                .Where(x => x.RequestId == created.Id)
                .OrderBy(x => x.Order)
                .FirstOrDefaultAsync(ct);

            var techId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var techName = user.Identity?.Name;
            var worklogNote = $"Approved AI automation using KB article '{article.Title}'. Created request {created.TrackingId}.";
            await sender.Send(new CreateWorkLogCommand(incident.Id, 0, worklogNote, techId, techName, NotifyCustomer: false), ct);
            var requestProvenanceNote =
                $"Created from AI-approved automation on incident {incident.TrackingId} using KB article '{article.Title}'.";
            await sender.Send(
                new CreateWorkLogCommand(
                    created.Id,
                    0,
                    requestProvenanceNote,
                    techId,
                    techName,
                    NotifyCustomer: false),
                ct);

            await aiAudit.RecordAsync(
                new AiOperationAuditEntry(
                    "ticket-automation-approved",
                    incident.OrganizationId,
                    "human-approved",
                    article.AutomationBindingId,
                    SubjectId: incident.Id,
                    Notes: created.Id),
                ct);

            await aiAudit.RecordAsync(
                new AiOperationAuditEntry(
                    "request-created-from-ticket-ai-automation",
                    created.OrganizationId,
                    "ticket-ai-approval",
                    article.AutomationBindingId ?? "automation-binding",
                    SubjectId: created.Id,
                    Notes: $"sourceTicket:{incident.Id};article:{article.Id}"),
                ct);

            return Results.Ok(new TicketAutomationApprovalResultDto
            {
                RequestId = created.Id,
                RequestTrackingId = created.TrackingId,
                RequestTaskId = createdTask?.Id,
                RequestTaskStatus = createdTask?.Status.ToString(),
                AutomationBindingId = article.AutomationBindingId!,
                ArticleId = article.Id.ToString()
            });
        })
        .WithSummary("Approve and start KB-linked automation for a ticket")
        .WithDescription("Creates a dedicated automation request/task from an incident only when the linked KB article has an approved automation binding and the request-form payload contract validates.");

        group.MapGet("/{id:guid}/automation-approvals/{articleId:guid}/validation", async (
            Guid id,
            Guid articleId,
            HelpdeskDbContext db,
            IRepository<Ticket> tickets,
            IRequestFormSchemaParser requestFormSchemaParser,
            IAutomationBindingPayloadContractService automationPayloadContractService,
            CancellationToken ct) =>
        {
            var ticket = await tickets.GetAsync(id.ToString());
            if (ticket is not Incident incident)
            {
                return Results.BadRequest(new TicketAutomationValidationDto
                {
                    ArticleId = articleId.ToString(),
                    CanApprove = false,
                    Summary = "Automation approval is currently supported for incidents only.",
                    Errors = ["Automation approval is currently supported for incidents only."]
                });
            }

            var validation = await ValidateTicketAutomationApprovalAsync(
                db,
                requestFormSchemaParser,
                automationPayloadContractService,
                incident,
                articleId.ToString(),
                ct);

            if (validation.Success)
            {
                return Results.Ok(new TicketAutomationValidationDto
                {
                    ArticleId = articleId.ToString(),
                    CanApprove = true,
                    Summary = "Automation target is ready for approval.",
                    Errors = []
                });
            }

            return Results.Json(
                new TicketAutomationValidationDto
                {
                    ArticleId = articleId.ToString(),
                    CanApprove = false,
                    Summary = validation.ErrorMessage ?? "Automation target is not ready for approval.",
                    Errors = validation.Errors.ToList()
                },
                statusCode: (int)validation.StatusCode);
        })
        .WithSummary("Preview KB-linked automation approval readiness")
        .WithDescription("Returns whether a KB-linked automation can be approved for the incident and includes any blocking validation errors.");

        group.MapGet("/{id:guid}/automation-approvals", async (
            Guid id,
            HelpdeskDbContext db,
            CancellationToken ct) =>
        {
            var ticketId = id.ToString();
            var ticketExists = await db.Tickets.AsNoTracking().AnyAsync(x => x.Id == ticketId, ct);
            if (!ticketExists)
            {
                return Results.NotFound();
            }

            var runs = await db.Requests
                .AsNoTracking()
                .Where(x => x.SourceTicketId == ticketId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TicketAutomationRunDto
                {
                    RequestId = x.Id,
                    RequestTrackingId = x.TrackingId,
                    RequestState = x.State.ToString(),
                    WorkflowStatus = x.WorkflowStatus,
                    CreatedAt = x.CreatedAt,
                    ArticleId = x.SourceKnowledgeArticleId.HasValue ? x.SourceKnowledgeArticleId.Value.ToString() : null,
                    AutomationBindingId = x.SourceAutomationBindingId,
                    ArticleTitle = db.KnowledgeBaseArticles
                        .Where(article => x.SourceKnowledgeArticleId.HasValue && article.Id == x.SourceKnowledgeArticleId.Value)
                        .Select(article => article.Title)
                        .FirstOrDefault(),
                    RequestTaskId = db.RequestTasks
                        .Where(task => task.RequestId == x.Id)
                        .OrderBy(task => task.Order)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => task.Id)
                        .FirstOrDefault(),
                    RequestTaskTitle = db.RequestTasks
                        .Where(task => task.RequestId == x.Id)
                        .OrderBy(task => task.Order)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => task.Title)
                        .FirstOrDefault(),
                    RequestTaskStatus = db.RequestTasks
                        .Where(task => task.RequestId == x.Id)
                        .OrderBy(task => task.Order)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => task.Status.ToString())
                        .FirstOrDefault(),
                    LastAutomationStatus = db.RequestTasks
                        .Where(task => task.RequestId == x.Id)
                        .OrderBy(task => task.Order)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => task.LastAutomationStatus)
                        .FirstOrDefault(),
                    OrchestrationRunId = db.RequestTasks
                        .Where(task => task.RequestId == x.Id)
                        .OrderBy(task => task.Order)
                        .ThenBy(task => task.CreatedAt)
                        .Select(task => task.OrchestrationExternalRunId ?? task.OrchestratorExecutionId)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return Results.Ok(runs);
        })
        .WithSummary("List AI-started automation runs for a ticket")
        .WithDescription("Returns automation requests/tasks that were started from AI approval for the selected ticket.");

        group.MapPost("/{id:guid}/generate-knowledge", async (
            Guid id,
            IRepository<Ticket> tickets,
            IKnowledgeBuilderService kbService,
            CancellationToken token,
            bool regenerate = false) =>
        {
            Console.WriteLine($"Looking up ticket with ID: {id}");

            var ticket = await tickets.GetAsync(id.ToString());
            if (ticket is null)
                return Results.NotFound();

            var article = await kbService.GenerateDraftFromResolvedTicketAsync(id.ToString(), token, regenerate);
            if (article is null)
                return Results.Problem("Ticket must be resolved before generating knowledge.", statusCode: 400);

            return Results.Ok(article);
        })
        .WithName("GenerateTicketKnowledge")
        .WithSummary("Generate knowledge base article from ticket")
        .WithDescription("Generates or regenerates a knowledge base article draft from a resolved ticket.");
    }

    public static RouteHandlerBuilder MapTicketCustomerEndpoint(RouteGroupBuilder ticketGroup)
    {
        return ticketGroup.MapPost("/customer", async (
            [FromRoute] string ticketType,
            [FromRoute] string ticketId,
            [FromBody] UpdateTicketCustomerRequest req,
            [FromServices] IRepository<Incident> incidents,
            [FromServices] IRepository<TicketRequest> requests,
            [FromServices] IRepository<Change> changes,
            [FromServices] IRepository<Customer> customers,
            ClaimsPrincipal user) =>
        {
            if (!IsTechnicianOrAdmin(user))
            {
                return Results.Forbid();
            }

            var ticket = await GetTicketAsync(ticketType, ticketId, incidents, requests, changes);
            if (ticket is null)
            {
                return Results.Problem("Ticket not found", statusCode: 404);
            }

            if (!string.IsNullOrWhiteSpace(ticket.CustomerId))
            {
                return Results.BadRequest("Ticket already has a requester.");
            }

            var validation = await ValidateCustomerAsync(customers, ticket.OrganizationId, req.CustomerId);
            if (validation.Result is not null)
            {
                return validation.Result;
            }

            var customer = validation.Customer!;
            ticket.CustomerId = customer.Id;
            ticket.RequesterEmail = customer.Email;
            ticket.UpdatedAt = DateTime.UtcNow;

            var updated = await UpdateTicketAsync(ticketType, ticket, incidents, requests, changes);
            if (!updated)
            {
                return Results.Problem("Ticket not found", statusCode: 404);
            }

            return Results.Ok(new
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerEmail = customer.Email
            });
        })
        .WithName("SetTicketCustomer")
        .WithSummary("Set the requester customer for a ticket")
        .WithDescription("Sets the customer user a ticket is for when the ticket has no requester yet.");
    }

    private static bool IsSupportedTicketType(string ticketType) =>
        string.Equals(ticketType, "incidents", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ticketType, "requests", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ticketType, "changes", StringComparison.OrdinalIgnoreCase);

    private static async Task<IResult?> AuthorizeTicketViewAsync(
        string ticketType,
        string ticketId,
        ClaimsPrincipal user,
        ICurrentUserAccessService accessService,
        HelpdeskDbContext db,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedTicketType(ticketType))
        {
            return Results.BadRequest("Unsupported ticketType. Use incidents, requests, or changes.");
        }

        var ticket = await db.Tickets.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var customer = !string.IsNullOrWhiteSpace(ticket.CustomerId)
            ? await db.Customers.AsNoTracking()
                .Where(candidate => candidate.Id == ticket.CustomerId)
                .Select(candidate => new { candidate.Id, candidate.Email })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var access = await accessService.ResolveAsync(user, cancellationToken);
        var canView = ticket switch
        {
            Incident => access.CanViewIncident(ticket.OrganizationId, customer?.Id ?? ticket.CustomerId, customer?.Email ?? ticket.RequesterEmail),
            TicketRequest => access.CanViewRequest(ticket.OrganizationId, customer?.Id ?? ticket.CustomerId, customer?.Email ?? ticket.RequesterEmail),
            Change => access.CanViewChange(ticket.OrganizationId, customer?.Id ?? ticket.CustomerId, customer?.Email ?? ticket.RequesterEmail),
            _ => false
        };
        return canView ? null : Results.Forbid();
    }

    private static bool IsTechnicianOrAdmin(ClaimsPrincipal user) =>
        user.IsInRole("HelpdeskAdmin") ||
        user.IsInRole("Technician") ||
        user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "roles") &&
            (string.Equals(c.Value, "HelpdeskAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(c.Value, "Technician", StringComparison.OrdinalIgnoreCase)));

    private static async Task<Ticket?> GetTicketAsync(
        string ticketType,
        string ticketId,
        IRepository<Incident> incidents,
        IRepository<TicketRequest> requests,
        IRepository<Change> changes) =>
        ticketType.ToLowerInvariant() switch
        {
            "incidents" => await incidents.GetAsync(ticketId),
            "requests" => await requests.GetAsync(ticketId),
            "changes" => await changes.GetAsync(ticketId),
            _ => null
        };

    private static async Task<bool> UpdateTicketAsync(
        string ticketType,
        Ticket ticket,
        IRepository<Incident> incidents,
        IRepository<TicketRequest> requests,
        IRepository<Change> changes) =>
        ticketType.ToLowerInvariant() switch
        {
            "incidents" when ticket is Incident incident => await incidents.UpdateAsync(incident) is not null,
            "requests" when ticket is TicketRequest request => await requests.UpdateAsync(request) is not null,
            "changes" when ticket is Change change => await changes.UpdateAsync(change) is not null,
            _ => false
        };

    private static async Task<(IResult? Result, Customer? Customer)> ValidateCustomerAsync(
        IRepository<Customer> customers,
        string? organizationId,
        string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return (Results.BadRequest("Customer is required."), null);
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return (Results.BadRequest("Organization is required."), null);
        }

        var customer = await customers.GetAsync(customerId);
        if (customer is null)
        {
            return (Results.BadRequest("Customer was not found."), null);
        }

        if (!customer.IsEnabled)
        {
            return (Results.BadRequest("Customer is disabled."), null);
        }

        if (!string.Equals(customer.OrganizationId, organizationId, StringComparison.OrdinalIgnoreCase))
        {
            return (Results.BadRequest("Customer must belong to the selected organization."), null);
        }

        return (null, customer);
    }

    public sealed record UpdateTicketCustomerRequest(string? CustomerId);

    private static async Task<TicketAutomationApprovalValidation> ValidateTicketAutomationApprovalAsync(
        HelpdeskDbContext db,
        IRequestFormSchemaParser requestFormSchemaParser,
        IAutomationBindingPayloadContractService automationPayloadContractService,
        Incident incident,
        string articleId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(articleId, out var articleGuid))
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.BadRequest, "ArticleId is required.");
        }

        var article = await db.KnowledgeBaseArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == articleGuid, ct);
        if (article is null)
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.NotFound, "Knowledge article not found.");
        }

        if (!string.Equals(article.OrganizationId, incident.OrganizationId, StringComparison.Ordinal))
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.BadRequest, "The selected knowledge article belongs to a different organization.");
        }

        if (string.IsNullOrWhiteSpace(article.AutomationBindingId)
            || string.IsNullOrWhiteSpace(article.AutomationRequestFormId)
            || article.AutomationTaskTemplateId is null)
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.BadRequest, "This knowledge article does not have an approved automation target.");
        }

        var binding = await db.AutomationBindings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == article.AutomationBindingId, ct);
        if (binding is null || !binding.Enabled)
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.BadRequest, "The linked automation binding is unavailable.");
        }

        var requestForm = await db.RequestForms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == article.AutomationRequestFormId, ct);
        if (requestForm is null)
        {
            return TicketAutomationApprovalValidation.Failed(HttpStatusCode.BadRequest, "The linked request form is unavailable.");
        }

        var schema = requestFormSchemaParser.Parse(requestForm.JsonSchema);
        var automationTasks = schema.Tasks
            .Where(x => string.Equals(x.Type, "automation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (schema.Tasks.Count != 1
            || automationTasks.Count != 1
            || automationTasks[0].Id != article.AutomationTaskTemplateId.Value)
        {
            return TicketAutomationApprovalValidation.Failed(
                HttpStatusCode.BadRequest,
                "AI automation approval currently supports only dedicated single-step automation request forms.");
        }

        var payloadJson = BuildIncidentAutomationPayloadJson(incident, schema.Fields);
        var payloadValidation = await automationPayloadContractService.ValidateBoundRequestPayloadAsync(requestForm, payloadJson, ct);
        if (!payloadValidation.Success)
        {
            return TicketAutomationApprovalValidation.Failed(
                HttpStatusCode.BadRequest,
                "Automation payload validation failed for the linked request form.",
                payloadValidation.Errors);
        }

        return TicketAutomationApprovalValidation.Passed(article, requestForm, payloadJson);
    }

    private static string BuildIncidentAutomationPayloadJson(
        Incident incident,
        IReadOnlyCollection<FormField> fields)
    {
        var payload = new JsonObject
        {
            ["ticketId"] = incident.Id,
            ["ticket_id"] = incident.Id,
            ["incidentId"] = incident.Id,
            ["incident_id"] = incident.Id,
            ["trackingId"] = incident.TrackingId,
            ["tracking_id"] = incident.TrackingId,
            ["ticketNumber"] = incident.TrackingId,
            ["ticket_number"] = incident.TrackingId,
            ["incidentNumber"] = incident.TrackingId,
            ["incident_number"] = incident.TrackingId,
            ["title"] = incident.Title,
            ["summary"] = incident.Title,
            ["subject"] = incident.Title,
            ["description"] = incident.Description,
            ["details"] = incident.Description,
            ["issue"] = incident.Description,
            ["requesterEmail"] = incident.RequesterEmail,
            ["requester_email"] = incident.RequesterEmail,
            ["requestorEmail"] = incident.RequesterEmail,
            ["requestor_email"] = incident.RequesterEmail,
            ["userEmail"] = incident.RequesterEmail,
            ["user_email"] = incident.RequesterEmail,
            ["organizationId"] = incident.OrganizationId,
            ["organization_id"] = incident.OrganizationId,
            ["tenantId"] = incident.OrganizationId,
            ["tenant_id"] = incident.OrganizationId,
            ["customerId"] = incident.CustomerId,
            ["customer_id"] = incident.CustomerId,
            ["serviceId"] = incident.ServiceId,
            ["service_id"] = incident.ServiceId,
            ["priority"] = incident.Priority.ToString(),
            ["severity"] = incident.Priority.ToString(),
            ["impact"] = incident.Impact,
            ["emailFrom"] = incident.EmailFrom,
            ["email_from"] = incident.EmailFrom,
            ["senderEmail"] = incident.EmailFrom,
            ["sender_email"] = incident.EmailFrom,
            ["assignedToId"] = incident.AssignedToId,
            ["assigned_to_id"] = incident.AssignedToId,
            ["linkedAssetIds"] = new JsonArray([.. incident.LinkedAssetIds.Select(x => JsonValue.Create(x))]),
            ["linked_asset_ids"] = new JsonArray([.. incident.LinkedAssetIds.Select(x => JsonValue.Create(x))])
        };

        if (incident.EmailReceivedUtc.HasValue)
        {
            payload["emailReceivedUtc"] = incident.EmailReceivedUtc.Value.ToString("O");
            payload["email_received_utc"] = incident.EmailReceivedUtc.Value.ToString("O");
            payload["receivedAt"] = incident.EmailReceivedUtc.Value.ToString("O");
            payload["received_at"] = incident.EmailReceivedUtc.Value.ToString("O");
        }

        if (incident.DueDate.HasValue)
        {
            payload["dueDate"] = incident.DueDate.Value.ToString("O");
            payload["due_date"] = incident.DueDate.Value.ToString("O");
            payload["dueAt"] = incident.DueDate.Value.ToString("O");
            payload["due_at"] = incident.DueDate.Value.ToString("O");
        }

        if (incident.ClosedAt.HasValue)
        {
            payload["closedAt"] = incident.ClosedAt.Value.ToString("O");
            payload["closed_at"] = incident.ClosedAt.Value.ToString("O");
            payload["resolvedAt"] = incident.ClosedAt.Value.ToString("O");
            payload["resolved_at"] = incident.ClosedAt.Value.ToString("O");
        }

        var fieldLookup = fields
            .Select((field, index) => new { Key = FormFieldKeyResolver.Resolve(field, index), Field = field })
            .ToDictionary(x => x.Key, x => x.Field, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in fieldLookup)
        {
            var key = entry.Key;
            if (payload.ContainsKey(key))
            {
                continue;
            }

            var mappedValue = ResolveIncidentFieldValue(key, entry.Value, incident);
            if (mappedValue is not null)
            {
                payload[key] = mappedValue;
            }
        }

        return payload.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static JsonNode? ResolveIncidentFieldValue(string key, FormField field, Incident incident)
    {
        var normalizedKey = NormalizeAutomationFieldKey(key);
        var normalizedLabel = NormalizeAutomationFieldKey(field.Label);

        return ResolveIncidentFieldValueCore(normalizedKey, incident)
            ?? ResolveIncidentFieldValueCore(normalizedLabel, incident);
    }

    private static JsonNode? ResolveIncidentFieldValueCore(string normalizedKey, Incident incident)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        if (MatchesAutomationField(normalizedKey, "ticketid", "incidentid", "caseid"))
        {
            return incident.Id;
        }

        if (MatchesAutomationField(normalizedKey, "trackingid", "ticketnumber", "incidentnumber", "reference", "referencenumber"))
        {
            return incident.TrackingId;
        }

        if (MatchesAutomationField(normalizedKey, "title", "summary", "subject", "shortdescription", "incidenttitle"))
        {
            return incident.Title;
        }

        if (MatchesAutomationField(normalizedKey, "description", "details", "issue", "problemdescription", "incidentdescription", "body"))
        {
            return incident.Description;
        }

        if (MatchesAutomationField(
            normalizedKey,
            "requesteremail",
            "requestoremail",
            "useremail",
            "employeeemail",
            "calleremail",
            "email",
            "mail",
            "upn",
            "userprincipalname"))
        {
            return incident.RequesterEmail;
        }

        if (MatchesAutomationField(normalizedKey, "organizationid", "tenantid", "companyid", "businessunitid"))
        {
            return incident.OrganizationId;
        }

        if (MatchesAutomationField(normalizedKey, "customerid", "customer", "requestercustomerid"))
        {
            return incident.CustomerId;
        }

        if (MatchesAutomationField(normalizedKey, "serviceid", "service", "serviceofferingid"))
        {
            return incident.ServiceId;
        }

        if (MatchesAutomationField(normalizedKey, "priority", "severity"))
        {
            return incident.Priority.ToString();
        }

        if (MatchesAutomationField(normalizedKey, "impact"))
        {
            return incident.Impact;
        }

        if (MatchesAutomationField(normalizedKey, "emailfrom", "senderemail", "sourceemail", "fromemail"))
        {
            return incident.EmailFrom;
        }

        if (MatchesAutomationField(normalizedKey, "emailreceivedutc", "receivedat", "emailreceivedat", "messageReceivedAt"))
        {
            return incident.EmailReceivedUtc?.ToString("O");
        }

        if (MatchesAutomationField(normalizedKey, "assignedtoid", "assigneeid", "technicianid", "ownerid"))
        {
            return incident.AssignedToId;
        }

        if (MatchesAutomationField(normalizedKey, "duedate", "dueat", "targetdate"))
        {
            return incident.DueDate?.ToString("O");
        }

        if (MatchesAutomationField(normalizedKey, "closedat", "resolvedat", "completedat"))
        {
            return incident.ClosedAt?.ToString("O");
        }

        if (MatchesAutomationField(normalizedKey, "linkedassetids", "assetids", "linkedassets"))
        {
            return new JsonArray([.. incident.LinkedAssetIds.Select(x => JsonValue.Create(x))]);
        }

        return null;
    }

    private static string NormalizeAutomationFieldKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return new string(key
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static bool MatchesAutomationField(string normalizedKey, params string[] aliases)
    {
        return aliases.Any(alias => string.Equals(normalizedKey, alias, StringComparison.Ordinal));
    }

    private sealed class TicketAutomationApprovalValidation
    {
        public bool Success { get; init; }
        public HttpStatusCode StatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = [];
        public KnowledgeBaseArticle? Article { get; init; }
        public RequestForm? RequestForm { get; init; }
        public string? PayloadJson { get; init; }

        public IResult ToHttpResult()
        {
            var message = Errors.Count > 0
                ? string.Join(Environment.NewLine, Errors)
                : ErrorMessage ?? "Automation approval validation failed.";

            return StatusCode == HttpStatusCode.NotFound
                ? Results.NotFound(message)
                : Results.BadRequest(message);
        }

        public static TicketAutomationApprovalValidation Passed(
            KnowledgeBaseArticle article,
            RequestForm requestForm,
            string payloadJson) => new()
            {
                Success = true,
                StatusCode = HttpStatusCode.OK,
                Article = article,
                RequestForm = requestForm,
                PayloadJson = payloadJson
            };

        public static TicketAutomationApprovalValidation Failed(
            HttpStatusCode statusCode,
            string errorMessage,
            IReadOnlyList<string>? errors = null) => new()
            {
                Success = false,
                StatusCode = statusCode,
                ErrorMessage = errorMessage,
                Errors = errors ?? [errorMessage]
            };
    }
}
