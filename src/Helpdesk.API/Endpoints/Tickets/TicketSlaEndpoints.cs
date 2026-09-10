using System.Security.Claims;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
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
            ITicketSlaService slaService,
            IRepository<Ticket> ticketRepo,
            ITicketSlaRepository ticketSlaRepository,
            ISlaEscalationEvaluator escalationEvaluator,
            ILoggerFactory loggerFactory) =>
        {
            if (!IsOperator(user))
            {
                return Results.Forbid();
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
                await slaService.PauseAsync(ticketId, userId, request.Reason.Trim());
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
            ITicketSlaService slaService,
            IRepository<Ticket> ticketRepo,
            ITicketSlaRepository ticketSlaRepository,
            ISlaEscalationEvaluator escalationEvaluator,
            ILoggerFactory loggerFactory) =>
        {
            if (!IsOperator(user))
            {
                return Results.Forbid();
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

            try
            {
                await slaService.ResumeAsync(ticketId, userId);
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

    private static bool IsOperator(ClaimsPrincipal user)
    {
        return user.IsInRole("HelpdeskAdmin") || user.IsInRole("Technician");
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
