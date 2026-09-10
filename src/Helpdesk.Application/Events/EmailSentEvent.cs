namespace Helpdesk.Application.Events;

public sealed record EmailSentEvent(
    string Recipient,
    string TemplateName,
    string? TenantId,
    string? Reference,
    string CorrelationId,
    bool Success,
    string? Details = null)
    : DomainEvent(
        EventType: "EmailSent",
        Source: "Infrastructure",
        TenantId: TenantId,
        EntityId: null,
        CorrelationId: CorrelationId,
        Reference: Reference,
        OccurredUtc: DateTime.UtcNow);
