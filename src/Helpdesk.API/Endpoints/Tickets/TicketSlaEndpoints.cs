using System.Security.Claims;
using Helpdesk.Application.Sla;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.API.Endpoints.Tickets;

public static class TicketSlaEndpoints
{
    public static void MapTicketSlaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tickets")
            .WithTags("Ticket SLA")
            .RequireAuthorization();

        group.MapPost("/{ticketId}/sla/pause", async (
            string ticketId,
            PauseSlaRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            ITicketSlaService slaService,
            IRepository<Ticket> ticketRepo,
            ITicketSlaRepository ticketSlaRepository,
            ISlaEscalationEvaluator escalationEvaluator,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeSlaMutationAsync(
                ticketId,
                context.User,
                accessService,
                db,
                cancellationToken);
            if (authorization.Failure is not null)
            {
                return authorization.Failure;
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 200)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["reason"] = ["Reason is required and must be 200 characters or less."]
                });
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

            try
            {
                await slaService.PauseAsync(authorization.Ticket!, userId, request.Reason.Trim());
                await TryEvaluateEscalationAsync(
                    ticketId,
                    ticketRepo,
                    ticketSlaRepository,
                    escalationEvaluator,
                    loggerFactory.CreateLogger("TicketSlaEndpoints"));
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem("Ticket not found.", statusCode: 404);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("PauseTicketSla")
        .WithSummary("Pause ticket SLA");

        group.MapPost("/{ticketId}/sla/resume", async (
            string ticketId,
            ResumeSlaRequest _,
            ClaimsPrincipal user,
            HttpContext context,
            ICurrentUserAccessService accessService,
            HelpdeskDbContext db,
            ITicketSlaService slaService,
            IRepository<Ticket> ticketRepo,
            ITicketSlaRepository ticketSlaRepository,
            ISlaEscalationEvaluator escalationEvaluator,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeSlaMutationAsync(
                ticketId,
                context.User,
                accessService,
                db,
                cancellationToken);
            if (authorization.Failure is not null)
            {
                return authorization.Failure;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

            try
            {
                await slaService.ResumeAsync(authorization.Ticket!, userId);
                await TryEvaluateEscalationAsync(
                    ticketId,
                    ticketRepo,
                    ticketSlaRepository,
                    escalationEvaluator,
                    loggerFactory.CreateLogger("TicketSlaEndpoints"));
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem("Ticket not found.", statusCode: 404);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("ResumeTicketSla")
        .WithSummary("Resume ticket SLA");
    }

    private static async Task<SlaMutationAuthorization> AuthorizeSlaMutationAsync(
        string ticketId,
        ClaimsPrincipal user,
        ICurrentUserAccessService accessService,
        HelpdeskDbContext db,
        CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return new(null, Results.NotFound());
        }

        var access = await accessService.ResolveAsync(user, cancellationToken);
        return CanManageSla(access, ticket)
            ? new(ticket, null)
            : new(null, Results.Forbid());
    }

    private sealed record SlaMutationAuthorization(Ticket? Ticket, IResult? Failure);

    private static bool CanManageSla(CurrentUserAccessProfile access, Ticket ticket)
    {
        return ticket switch
        {
            Incident => access.CanManageIncident(ticket.OrganizationId),
            Request or RequestTask => access.CanManageRequest(ticket.OrganizationId),
            Change => access.CanManageChange(ticket.OrganizationId),
            _ => false
        };
    }

    private static async Task TryEvaluateEscalationAsync(
        string ticketId,
        IRepository<Ticket> ticketRepo,
        ITicketSlaRepository ticketSlaRepository,
        ISlaEscalationEvaluator escalationEvaluator,
        ILogger logger)
    {
        try
        {
            var ticket = await ticketRepo.GetAsync(ticketId);
            if (ticket is null)
            {
                return;
            }

            var slaState = await ticketSlaRepository.GetByTicketIdAsync(ticketId);
            if (slaState is null)
            {
                return;
            }

            await escalationEvaluator.EvaluateAndNotifyAsync(ticket, slaState, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SLA escalation evaluation failed for ticket {TicketId}.", ticketId);
        }
    }
}
