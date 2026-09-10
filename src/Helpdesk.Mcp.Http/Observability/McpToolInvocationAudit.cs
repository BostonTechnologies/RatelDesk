using Helpdesk.Mcp.Configuration;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace Helpdesk.Mcp.Http.Observability;

/// <summary>
/// Emits bounded, structured diagnostics for parsed MCP tool calls. It never
/// inspects raw HTTP request bodies or authorization headers.
/// </summary>
public sealed class McpToolInvocationAudit(
    HelpdeskMcpHostContext hostContext,
    ILogger<McpToolInvocationAudit> logger)
{
    public async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            var details = ResultDetails(result);
            logger.LogInformation(
                "MCP tool invocation completed. Instance={Instance} Transport={Transport} Tool={Tool} Operation={Operation} Caller={Caller} Success={Success} ResultStatus={ResultStatus} CorrelationId={CorrelationId} DurationMs={DurationMs} TraceId={TraceId}",
                hostContext.Instance,
                hostContext.Transport,
                Token(context.Params.Name),
                Operation(context.Params.Arguments),
                Caller(context.User),
                result.IsError is not true,
                details.Status,
                details.CorrelationId,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                Activity.Current?.TraceId.ToString());
            return result;
        }
        catch (Exception)
        {
            logger.LogWarning(
                "MCP tool invocation failed. Instance={Instance} Transport={Transport} Tool={Tool} Operation={Operation} Caller={Caller} DurationMs={DurationMs} TraceId={TraceId}",
                hostContext.Instance,
                hostContext.Transport,
                Token(context.Params.Name),
                Operation(context.Params.Arguments),
                Caller(context.User),
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                Activity.Current?.TraceId.ToString());
            throw;
        }
    }

    private static (string Status, string CorrelationId) ResultDetails(CallToolResult result)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } content)
            return (result.IsError is true ? "error" : "unknown", "none");

        return (Token(content, "status"), Token(content, "correlationId"));
    }

    private static string Operation(IDictionary<string, JsonElement>? arguments)
        => arguments is not null && arguments.TryGetValue("operation", out var operation) && operation.ValueKind == JsonValueKind.String
            ? Token(operation.GetString())
            : "unknown";

    private static string Caller(ClaimsPrincipal? user)
        => Token(user?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? user?.FindFirstValue("sub")
                   ?? user?.FindFirstValue("client_id")
                   ?? user?.FindFirstValue("azp")
                   ?? "anonymous");

    private static string Token(JsonElement objectValue, string property)
        => objectValue.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? Token(value.GetString())
            : "unknown";

    private static string Token(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            return "unknown";

        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '/' or '.')
            ? value
            : "unknown";
    }
}
