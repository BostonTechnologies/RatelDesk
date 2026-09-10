using System.Security.Claims;
using System.Text.Json;
using Helpdesk.Application.AiAssistant;
using Helpdesk.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Endpoints.AiAssistant;

public static class AiAssistantAiAssistantEndpoints
{
    public static void MapAiAssistantAiAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/v1/admin/ai-assistant-webhooks").WithTags("AiAssistant Webhooks").RequireAuthorization("HelpdeskAdmin");
        admin.MapGet("/", async (string organizationId, IAiAssistantAiAssistantService service, CancellationToken ct) => Results.Ok(await service.ListConfigurationsAsync(organizationId, ct)));
        admin.MapPost("/", async (UpsertAiAssistantWebhookConfigurationDto dto, HttpContext ctx, IAiAssistantAiAssistantService service, CancellationToken ct) => await UpsertAsync(null, dto, ctx, service, ct, true));
        admin.MapPut("/{id:guid}", async (Guid id, UpsertAiAssistantWebhookConfigurationDto dto, HttpContext ctx, IAiAssistantAiAssistantService service, CancellationToken ct) => await UpsertAsync(id, dto, ctx, service, ct, false));
        admin.MapPost("/{id:guid}/state", async (Guid id, AiAssistantWebhookStateDto dto, HttpContext ctx, IAiAssistantAiAssistantService service, CancellationToken ct) => { try { await service.SetConfigurationStateAsync(id, dto.OrganizationId, dto.Enabled, dto.Archived, Actor(ctx), ct); return Results.NoContent(); } catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["organizationId"] = [ex.Message] }); } });

        var tickets = app.MapGroup("/api/v1/{ticketType}/{ticketId}/ai-assistant").WithTags("AI Assistant");
        tickets.MapGet("/eligible-configurations", async (string ticketType, string ticketId, IAiAssistantAiAssistantService service, CancellationToken ct) => Results.Ok(await service.GetEligibleAsync(ticketId, ticketType, ct))).RequireAuthorization("HelpdeskStaff");
        tickets.MapPost("/investigations", async (string ticketType, string ticketId, DispatchAiInvestigationDto dto, HttpContext ctx, IAiAssistantAiAssistantService service, CancellationToken ct) => await DispatchAsync(ticketType, ticketId, dto, ctx, service, ct)).RequireAuthorization("HelpdeskStaff");
        tickets.MapGet("/worklog", async (string ticketType, string ticketId, IAiAssistantAiAssistantService service, CancellationToken ct) => Results.Ok(await service.GetWorklogAsync(ticketId, ticketType, ct))).RequireAuthorization("HelpdeskStaff");
        tickets.MapGet("/worklog/stream", StreamAsync).RequireAuthorization("HelpdeskStaff");

        var mcp = app.MapGroup("/api/v1/ai-assistant/investigations").WithTags("AI Assistant MCP").RequireAuthorization("AuthentikAiAgentApi");
        mcp.MapPost("/{invocationId:guid}/worklog", async (Guid invocationId, AppendAiInvestigationWorklogDto dto, IAiAssistantAiAssistantService service, CancellationToken ct) => await service.AppendMcpWorklogAsync(invocationId, dto, ct) ? Results.NoContent() : Results.BadRequest());
    }

    private static async Task StreamAsync(string ticketId, HttpContext context, IAiInvestigationEventBus bus, CancellationToken ct)
    {
        context.Response.Headers.ContentType = "text/event-stream"; context.Response.Headers.CacheControl = "no-cache"; context.Response.Headers.Connection = "keep-alive";
        var reader = bus.Subscribe(ticketId);
        try { await foreach (var item in reader.ReadAllAsync(ct)) { await context.Response.WriteAsync($"event: ai-worklog\ndata: {JsonSerializer.Serialize(item)}\n\n", ct); await context.Response.Body.FlushAsync(ct); } }
        finally { bus.Unsubscribe(ticketId, reader); }
    }
    private static async Task<IResult> UpsertAsync(Guid? id, UpsertAiAssistantWebhookConfigurationDto dto, HttpContext context, IAiAssistantAiAssistantService service, CancellationToken ct, bool isCreate)
    {
        try
        {
            var configuration = await service.UpsertConfigurationAsync(id, dto, Actor(context), ct);
            return isCreate ? Results.Created($"/api/v1/admin/ai-assistant-webhooks/{configuration.Id}", configuration) : Results.Ok(configuration);
        }
        catch (ArgumentException ex)
        {
            var field = ex.Message.Contains("tenant", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("organization", StringComparison.OrdinalIgnoreCase)
                ? "organizationId"
                : "endpoint";
            return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [ex.Message] });
        }
    }
    private static async Task<IResult> DispatchAsync(string ticketType, string ticketId, DispatchAiInvestigationDto dto, HttpContext context, IAiAssistantAiAssistantService service, CancellationToken ct)
    {
        try
        {
            var invocation = await service.DispatchAsync(ticketId, ticketType, dto, Actor(context), ct);
            return Results.Accepted($"/api/v1/{ticketType}/{ticketId}/ai-assistant/investigations", invocation);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["operatorAssistanceRequest"] = [ex.Message] });
        }
    }
    private static string Actor(HttpContext ctx) => ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub") ?? "unknown";
}
