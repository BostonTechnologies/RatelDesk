using System.Diagnostics;
using System.Diagnostics.Metrics;
using Helpdesk.Shared.DTOs.Notification;

namespace Helpdesk.Application.Observability;

public static class HelpdeskTelemetry
{
    public const string ActivitySourceName = "Helpdesk";
    public const string MeterName = "Helpdesk";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> NotificationsCreated = Meter.CreateCounter<long>(
        "helpdesk_notifications_created_total",
        description: "Notifications created by severity, category, source, and global scope.");
    private static readonly Counter<long> NotificationStreamConnections = Meter.CreateCounter<long>(
        "helpdesk_notification_stream_connections_total",
        description: "Notification SSE stream connections.");
    private static readonly Counter<long> NotificationStreamDisconnects = Meter.CreateCounter<long>(
        "helpdesk_notification_stream_disconnects_total",
        description: "Notification SSE stream disconnects.");
    private static readonly Counter<long> NotificationStreamEventsSent = Meter.CreateCounter<long>(
        "helpdesk_notification_stream_events_sent_total",
        description: "Notification SSE events sent to connected clients.");
    private static readonly Counter<long> NotificationStreamErrors = Meter.CreateCounter<long>(
        "helpdesk_notification_stream_errors_total",
        description: "Notification SSE stream errors.");
    private static readonly Counter<long> AiPostProcessOutcomes = Meter.CreateCounter<long>(
        "helpdesk_ai_postprocess_total",
        description: "Resolved ticket AI post-processing outcomes.");
    private static readonly Counter<long> EmailDeliveryAttempts = Meter.CreateCounter<long>(
        "helpdesk_email_delivery_attempts_total",
        description: "Ticket email delivery attempts by source, provider, and status.");

    public static Activity? StartActivity(string name)
        => ActivitySource.StartActivity(name);

    public static void RecordNotificationCreated(CreateNotificationRequest request)
    {
        NotificationsCreated.Add(1, NotificationTags(request));
    }

    public static void RecordNotificationStreamConnected(string? tenantId, string? category)
    {
        NotificationStreamConnections.Add(1, StreamTags(tenantId, category));
    }

    public static void RecordNotificationStreamDisconnected(string? tenantId, string? category)
    {
        NotificationStreamDisconnects.Add(1, StreamTags(tenantId, category));
    }

    public static void RecordNotificationStreamEventSent(string? tenantId, string? category)
    {
        NotificationStreamEventsSent.Add(1, StreamTags(tenantId, category));
    }

    public static void RecordNotificationStreamError(string? tenantId, string? category)
    {
        NotificationStreamErrors.Add(1, StreamTags(tenantId, category));
    }

    public static void RecordAiPostProcessOutcome(string outcome, string reason)
    {
        AiPostProcessOutcomes.Add(1,
            new KeyValuePair<string, object?>("outcome", NormalizeTagValue(outcome, "unknown")),
            new KeyValuePair<string, object?>("reason", NormalizeTagValue(reason, "unknown")));
    }

    public static void RecordEmailDeliveryAttempt(string source, string provider, string status)
    {
        EmailDeliveryAttempts.Add(1,
            new KeyValuePair<string, object?>("source", NormalizeTagValue(source, "unknown")),
            new KeyValuePair<string, object?>("provider", NormalizeTagValue(provider, "unknown")),
            new KeyValuePair<string, object?>("status", NormalizeTagValue(status, "unknown")));
    }

    private static KeyValuePair<string, object?>[] NotificationTags(CreateNotificationRequest request)
        =>
        [
            new("severity", request.Severity.ToString()),
            new("category", NormalizeTagValue(request.Category, "uncategorized")),
            new("source", NormalizeTagValue(request.Source, "unknown")),
            new("is_global", request.IsGlobal().ToString().ToLowerInvariant())
        ];

    private static KeyValuePair<string, object?>[] StreamTags(string? tenantId, string? category)
        =>
        [
            new("tenant_scope", string.IsNullOrWhiteSpace(tenantId) ? "all" : "tenant"),
            new("category", NormalizeTagValue(category, "all"))
        ];

    private static bool IsGlobal(this CreateNotificationRequest request)
        => string.IsNullOrWhiteSpace(request.UserId);

    private static string NormalizeTagValue(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
