using Helpdesk.Shared.Enums;

namespace Helpdesk.Application.Events;

public static class SlaTriggerSources
{
    public const string Manual = "Manual";
    public const string Worklog = "Worklog";
    public const string Hangfire = "Hangfire";
    public const string System = "System";
}

public sealed record SlaPolicyCreatedDomainEvent(
    string PolicyId,
    string? TenantId,
    SlaScopeType ScopeType,
    TicketType AppliesToTicketType,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaPolicyCreatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: PolicyId,
        CorrelationId: CorrelationId,
        Reference: PolicyId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaPolicyUpdatedDomainEvent(
    string PolicyId,
    string? TenantId,
    SlaScopeType ScopeType,
    TicketType AppliesToTicketType,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaPolicyUpdatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: PolicyId,
        CorrelationId: CorrelationId,
        Reference: PolicyId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaPolicyDeletedDomainEvent(
    string PolicyId,
    string? TenantId,
    SlaScopeType ScopeType,
    TicketType AppliesToTicketType,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaPolicyDeletedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: PolicyId,
        CorrelationId: CorrelationId,
        Reference: PolicyId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaPolicyActivatedDomainEvent(
    string PolicyId,
    string? TenantId,
    SlaScopeType ScopeType,
    TicketType AppliesToTicketType,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaPolicyActivatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: PolicyId,
        CorrelationId: CorrelationId,
        Reference: PolicyId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaPolicyDeactivatedDomainEvent(
    string PolicyId,
    string? TenantId,
    SlaScopeType ScopeType,
    TicketType AppliesToTicketType,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaPolicyDeactivatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: PolicyId,
        CorrelationId: CorrelationId,
        Reference: PolicyId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkingCalendarCreatedDomainEvent(
    string CalendarId,
    string? TenantId,
    string TimeZoneId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(WorkingCalendarCreatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: CalendarId,
        CorrelationId: CorrelationId,
        Reference: CalendarId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkingCalendarUpdatedDomainEvent(
    string CalendarId,
    string? TenantId,
    string TimeZoneId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(WorkingCalendarUpdatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: CalendarId,
        CorrelationId: CorrelationId,
        Reference: CalendarId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkingCalendarDeletedDomainEvent(
    string CalendarId,
    string? TenantId,
    string TimeZoneId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(WorkingCalendarDeletedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: CalendarId,
        CorrelationId: CorrelationId,
        Reference: CalendarId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record WorkingCalendarActivatedDomainEvent(
    string CalendarId,
    string? TenantId,
    string TimeZoneId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(WorkingCalendarActivatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: CalendarId,
        CorrelationId: CorrelationId,
        Reference: CalendarId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TenantSlaSettingsUpdatedDomainEvent(
    string TenantId,
    bool UseBusinessHours,
    string? CalendarId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TenantSlaSettingsUpdatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TenantId,
        CorrelationId: CorrelationId,
        Reference: TenantId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record BusinessHoursEnabledDomainEvent(
    string TenantId,
    bool UseBusinessHours,
    string? CalendarId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(BusinessHoursEnabledDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TenantId,
        CorrelationId: CorrelationId,
        Reference: TenantId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record BusinessHoursDisabledDomainEvent(
    string TenantId,
    bool UseBusinessHours,
    string? CalendarId,
    string? ChangedByUserId,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(BusinessHoursDisabledDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TenantId,
        CorrelationId: CorrelationId,
        Reference: TenantId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaInitializedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaInitializedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaPausedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaPausedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaResumedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaResumedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaBreachedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaBreachedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaCompletedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaCompletedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaAutoResumedDomainEvent(
    string TicketId,
    string? TenantId,
    SlaMetricType? Metric,
    SlaStatus Status,
    DateTimeOffset Timestamp,
    string TriggerSource,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaAutoResumedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaEscalationTriggeredDomainEvent(
    string TicketId,
    SlaMetricType Metric,
    int TriggerPercent,
    int RecipientsResolvedCount,
    int AttemptCount,
    string? Error,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaEscalationTriggeredDomainEvent),
        Source: "SLA",
        TenantId: null,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaEscalationSentDomainEvent(
    string TicketId,
    SlaMetricType Metric,
    int TriggerPercent,
    int RecipientsResolvedCount,
    int AttemptCount,
    string? Error,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaEscalationSentDomainEvent),
        Source: "SLA",
        TenantId: null,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record TicketSlaEscalationFailedDomainEvent(
    string TicketId,
    SlaMetricType Metric,
    int TriggerPercent,
    int RecipientsResolvedCount,
    int AttemptCount,
    string? Error,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(TicketSlaEscalationFailedDomainEvent),
        Source: "SLA",
        TenantId: null,
        EntityId: TicketId,
        CorrelationId: CorrelationId,
        Reference: TicketId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportSubscriptionCreatedDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportSubscriptionCreatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportSubscriptionUpdatedDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportSubscriptionUpdatedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportSubscriptionDeletedDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportSubscriptionDeletedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportGeneratedDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportGeneratedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportSentDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportSentDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaReportSendFailedDomainEvent(
    string SubscriptionId,
    string TenantId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int RecipientsCount,
    int AttemptCount,
    string? Error,
    DateTimeOffset Timestamp,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaReportSendFailedDomainEvent),
        Source: "SLA",
        TenantId: TenantId,
        EntityId: SubscriptionId,
        CorrelationId: CorrelationId,
        Reference: SubscriptionId,
        OccurredUtc: Timestamp.UtcDateTime);

public sealed record SlaEvaluationJobStartedDomainEvent(
    string JobId,
    DateTimeOffset StartedAt,
    long DurationMs,
    int TicketsProcessed,
    int EscalationsSent,
    int ErrorsCount,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaEvaluationJobStartedDomainEvent),
        Source: "Hangfire",
        TenantId: null,
        EntityId: JobId,
        CorrelationId: CorrelationId,
        Reference: JobId,
        OccurredUtc: StartedAt.UtcDateTime);

public sealed record SlaEvaluationJobCompletedDomainEvent(
    string JobId,
    DateTimeOffset StartedAt,
    long DurationMs,
    int TicketsProcessed,
    int EscalationsSent,
    int ErrorsCount,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaEvaluationJobCompletedDomainEvent),
        Source: "Hangfire",
        TenantId: null,
        EntityId: JobId,
        CorrelationId: CorrelationId,
        Reference: JobId,
        OccurredUtc: StartedAt.UtcDateTime);

public sealed record SlaEvaluationJobFailedDomainEvent(
    string JobId,
    DateTimeOffset StartedAt,
    long DurationMs,
    int TicketsProcessed,
    int EscalationsSent,
    int ErrorsCount,
    string? Error,
    string CorrelationId)
    : DomainEvent(
        EventType: nameof(SlaEvaluationJobFailedDomainEvent),
        Source: "Hangfire",
        TenantId: null,
        EntityId: JobId,
        CorrelationId: CorrelationId,
        Reference: JobId,
        OccurredUtc: StartedAt.UtcDateTime);
