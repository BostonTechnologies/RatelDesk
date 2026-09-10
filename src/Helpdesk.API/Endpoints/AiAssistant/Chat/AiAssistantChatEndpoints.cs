using System.Security.Claims;
using System.Text.Json;
using Helpdesk.Application.AiAssistant.Chat;
using Helpdesk.Infrastructure.AiAssistant.Chat;
using Helpdesk.Shared.AiAssistant.Chat;
using Microsoft.Extensions.Options;

namespace Helpdesk.API.Endpoints.AiAssistant.Chat;

public static class AiAssistantChatEndpoints
{
    public static void MapAiAssistantChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{ticketType}/{ticketId}/ai-assistant/chat").RequireAuthorization("HelpdeskStaff").WithTags("AiAssistant Chat");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!context.HttpContext.RequestServices.GetRequiredService<IOptions<AiAssistantChatOptions>>().Value.Enabled)
                return Results.Problem("Chat is not enabled.", statusCode: 503);
            try { return await next(context); }
            catch (ChatConflictException ex) { return Results.Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: 400); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        });
        group.MapGet("", (string ticketType, string ticketId, Guid? conversationId, long? cursor, HttpContext context, IAiAssistantChatStore store, CancellationToken ct) => store.LoadAsync(ticketType, ticketId, conversationId, cursor ?? 0, Actor(context), ct)).WithName("GetAiAssistantChat").WithSummary("Load authorized ticket conversation events");
        group.MapGet("/history", (string ticketType, string ticketId, int? skip, IAiAssistantChatStore store, CancellationToken ct) => store.HistoryAsync(ticketType, ticketId, skip ?? 0, ct))
            .WithName("GetAiAssistantChatHistory").WithSummary("List authorized ticket conversations").WithDescription("Returns up to 50 conversations, active first; use skip for older pages. No daemon credentials or session paths are returned.");
        group.MapPost("/reconcile", async (string ticketType, string ticketId, ChatNewRequest request, HttpContext context, IAiAssistantChatStore store, IAiAssistantChatTransport transport, CancellationToken ct) =>
        {
            await store.RequestRecoveryAsync(ticketType, ticketId, request.ConversationId, Actor(context), ct);
            await transport.ReconcileAsync(request.ConversationId, ct);
            return Results.Accepted();
        }).WithName("ReconcileAiAssistantChat").WithSummary("Check an unresolved turn without resending").WithDescription("Audits the requesting operator, resumes the saved SignalR session and conservatively reconciles history.");
        group.MapPost("/abandon", async (string ticketType, string ticketId, ChatAbandonRequest request, HttpContext context, IAiAssistantChatStore store, CancellationToken ct) =>
        {
            await store.AbandonAsync(ticketType, ticketId, request, Actor(context), ct);
            return Results.Accepted();
        }).WithName("AbandonAiAssistantChat").WithSummary("Deliberately abandon unresolved delivery and start a blank conversation").WithDescription("Requires acknowledgement that the old remote turn may still execute. Audited, idempotent and transactionally serialized; never resends an old message.");
        group.MapPost("/messages", async (string ticketType, string ticketId, ChatMessageRequest request, HttpContext context, IAiAssistantChatStore store, IAiAssistantChatTransport transport, CancellationToken ct) =>
        {
            if (await store.AcceptMessageAsync(ticketType, ticketId, request, Actor(context), ct))
                await transport.SendAsync(request.ConversationId, request.ClientMessageId, request.Text, ct);
            return Results.Accepted();
        }).WithName("SendAiAssistantChatMessage").WithSummary("Admit one idempotent operator message");
        group.MapPost("/interactions/{callId}", async (string ticketType, string ticketId, string callId, ChatApprovalRequest request, HttpContext context, IAiAssistantChatStore store, IAiAssistantChatTransport transport, CancellationToken ct) =>
        {
            await store.AcceptApprovalAsync(ticketType, ticketId, callId, request, Actor(context), ct);
            await transport.RespondAsync(request.ConversationId, callId, request.SelectedKey, ct);
            return Results.Accepted();
        }).WithName("ApproveAiAssistantChatInteraction").WithSummary("Select an offered interaction option once");
        group.MapPost("/new", (string ticketType, string ticketId, ChatNewRequest request, HttpContext context, IAiAssistantChatStore store, CancellationToken ct) => store.NewAsync(ticketType, ticketId, request.ConversationId, Actor(context), ct)).WithName("NewAiAssistantChat").WithSummary("Archive an idle conversation and start a new one");
        group.MapGet("/stream", StreamAsync).WithName("StreamAiAssistantChat").WithSummary("Replay durable conversation events and stream live text");
    }

    private static string Actor(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub") ?? throw new UnauthorizedAccessException();

    private static async Task StreamAsync(string ticketType, string ticketId, Guid conversationId, long? cursor, HttpContext context, IChatLiveFeed feed, IServiceScopeFactory scopes, CancellationToken ct)
    {
        var header = context.Request.Headers["Last-Event-ID"].ToString();
        var position = cursor ?? 0;
        if (header.Length > 0 && !long.TryParse(header, out position)) throw new ArgumentException("Invalid Last-Event-ID.");
        var reader = feed.Subscribe(conversationId);
        try
        {
            var started = false;
            while (!ct.IsCancellationRequested)
            {
                ChatSnapshot snapshot;
                // Fresh scope avoids stale tracked state during a long-lived response.
                await using (var scope = scopes.CreateAsyncScope())
                    snapshot = await scope.ServiceProvider.GetRequiredService<IAiAssistantChatStore>().LoadAsync(ticketType, ticketId, conversationId, position, Actor(context), ct);
                if (!started)
                {
                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers["X-Accel-Buffering"] = "no";
                    started = true;
                }
                foreach (var item in snapshot.Events)
                {
                    await context.Response.WriteAsync($"id: {item.Sequence}\nevent: chat\ndata: {JsonSerializer.Serialize(item)}\n\n", ct);
                    position = item.Sequence;
                }
                while (reader.TryRead(out var delta))
                    if (delta.Text is not null && delta.AfterSequence == position && snapshot.State is ChatState.Processing or ChatState.AwaitingApproval)
                        await context.Response.WriteAsync($"event: text-delta\ndata: {JsonSerializer.Serialize(delta)}\n\n", ct);
                await context.Response.WriteAsync($"event: state\ndata: {JsonSerializer.Serialize(snapshot.State)}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
                if (position < snapshot.LastSequence) continue;
                // Notifications reduce latency; polling recovers a commit whose notification was lost.
                using var wake = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wake.CancelAfter(TimeSpan.FromSeconds(5));
                try { await reader.WaitToReadAsync(wake.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { continue; }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        finally { feed.Unsubscribe(conversationId, reader); }
    }
}
