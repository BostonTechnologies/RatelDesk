using System.Diagnostics;
using System.Security.Claims;
using Helpdesk.Application.Events;

namespace Helpdesk.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationConstants.HeaderName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? $"corr-{Guid.NewGuid():N}"
            : incoming!;
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? string.Empty;
        var spanId = activity?.SpanId.ToString() ?? string.Empty;
        var tenantId = context.User.FindFirstValue("tid");
        var smokeScenarioId = context.Request.Headers[CorrelationConstants.SmokeScenarioHeaderName].FirstOrDefault();

        context.Items[CorrelationConstants.HttpContextItemKey] = correlationId;
        if (!string.IsNullOrWhiteSpace(smokeScenarioId))
        {
            context.Items[CorrelationConstants.SmokeScenarioHttpContextItemKey] = smokeScenarioId;
            context.Response.Headers[CorrelationConstants.SmokeScenarioHeaderName] = smokeScenarioId;
        }

        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        activity?.SetTag("correlation_id", correlationId);
        activity?.SetTag("tenant_id", tenantId);
        if (!string.IsNullOrWhiteSpace(smokeScenarioId))
        {
            activity?.SetTag("helpdesk.smoke_scenario", smokeScenarioId);
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId,
            ["trace_id"] = traceId,
            ["span_id"] = spanId,
            ["tenant_id"] = tenantId,
            ["helpdesk_smoke_scenario"] = smokeScenarioId
        }))
        {
            await _next(context);
        }
    }
}
