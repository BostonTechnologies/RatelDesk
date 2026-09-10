using Helpdesk.Application.Notifications;
using Helpdesk.Application.Events;
using Helpdesk.Shared.DTOs.Notification;
using System.Security.Claims;

namespace Helpdesk.API.Middleware;

public class ExceptionNotificationMiddleware(RequestDelegate next, ILogger<ExceptionNotificationMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionNotificationMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, INotificationService notifications)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationConstants.HttpContextItemKey] as string;
            var smokeScenarioId = context.Items[CorrelationConstants.SmokeScenarioHttpContextItemKey] as string;
            var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
            var spanId = System.Diagnostics.Activity.Current?.SpanId.ToString();
            var tenantId = ResolveTenantId(context.User);

            _logger.LogError(
                ex,
                "Unhandled exception at path {Path}. CorrelationId={CorrelationId} TraceId={TraceId} SpanId={SpanId} TenantId={TenantId} SmokeScenario={SmokeScenario}",
                context.Request.Path,
                correlationId,
                traceId,
                spanId,
                tenantId,
                smokeScenarioId);

            try
            {
                var message = $"Unhandled exception at '{context.Request.Path}'.{Environment.NewLine}" +
                              $"CorrelationId: {correlationId ?? "unknown"}{Environment.NewLine}" +
                              $"TraceId: {traceId ?? "unknown"}{Environment.NewLine}" +
                              $"SpanId: {spanId ?? "unknown"}{Environment.NewLine}" +
                              $"TenantId: {tenantId ?? "unknown"}{Environment.NewLine}" +
                              $"SmokeScenario: {smokeScenarioId ?? "none"}{Environment.NewLine}" +
                              $"Message: {ex.Message}{Environment.NewLine}" +
                              $"StackTrace: {ex.StackTrace}";

                await notifications.CreateNotificationAsync(new CreateNotificationRequest
                {
                    Title = "UnhandledException",
                    Message = message,
                    Severity = NotificationSeverity.Critical,
                    Source = nameof(ExceptionNotificationMiddleware),
                    Category = "System",
                    TenantId = tenantId
                }, context.RequestAborted);
            }
            catch
            {
                // Swallow to keep exception pipeline stable.
            }

            throw;
        }
    }

    private static string? ResolveTenantId(ClaimsPrincipal user) =>
        FirstClaim(user, "organization_id", "tenant_id", "allowed_organization_id", "tid");

    private static string? FirstClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
