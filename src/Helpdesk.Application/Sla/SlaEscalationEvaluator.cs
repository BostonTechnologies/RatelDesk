using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Application.Events;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Sla;

public class SlaEscalationEvaluator(
    ISlaPolicyResolver policyResolver,
    ISlaClockService clockService,
    ITicketSlaEscalationEventRepository escalationEvents,
    ISlaEmailTemplate emailTemplate,
    IEmailSender emailSender,
    ILogger<SlaEscalationEvaluator> logger,
    IRecipientResolver? recipientResolver = null,
    IWorkingCalendarResolver? calendarResolver = null,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ISlaEscalationEvaluator
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromMinutes(10);
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task EvaluateAndNotifyAsync(Ticket ticket, TicketSlaState slaState, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        if (ticket.State == TicketState.Resolved)
        {
            return;
        }

        var ticketType = ResolveTicketType(ticket);
        if (ticketType is null)
        {
            return;
        }

        var policy = await policyResolver.ResolveAsync(ticket.OrganizationId, ticketType.Value, (int)ticket.Priority, ticket.ServiceId);
        policy ??= await policyResolver.ResolveAsync(ticket.OrganizationId, ticketType.Value);
        if (policy is null)
        {
            return;
        }

        var activeRules = policy.Escalations
            .Where(x => x.IsActive)
            .ToList();
        if (activeRules.Count == 0)
        {
            return;
        }

        var resolver = calendarResolver ?? new NullWorkingCalendarResolver();
        var recipientsResolver = recipientResolver ?? new BasicRecipientResolver();
        var calendar = slaState.IsBusinessHours
            ? await resolver.ResolveAsync(ticket.OrganizationId)
            : null;
        var snapshot = clockService.Compute(slaState, nowUtc, calendar);
        foreach (var rule in activeRules)
        {
            var currentPercent = rule.Metric == SlaMetricType.Response
                ? snapshot.ResponsePercentUsed
                : snapshot.ResolutionPercentUsed;
            if (currentPercent < rule.TriggerPercent)
            {
                continue;
            }

            var targets = rule.Targets.Count > 0
                ? rule.Targets
                : rule.Recipients
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new RecipientTarget
                    {
                        Type = RecipientTargetType.Email,
                        Value = x.Trim()
                    })
                    .ToList();
            var recipients = string.IsNullOrWhiteSpace(ticket.OrganizationId)
                ? new List<string>()
                : await recipientsResolver.ResolveEmailsAsync(ticket.OrganizationId, targets, ct);
            if (recipients.Count == 0)
            {
                await MarkNoRecipientsFailureAsync(ticket, policy, rule, nowUtc, ct);
                continue;
            }

            var seed = new TicketSlaEscalationEvent
            {
                TicketId = ticket.Id,
                Metric = rule.Metric,
                TriggerPercent = rule.TriggerPercent,
                PolicyId = policy.Id,
                RecipientsCsv = string.Join(",", recipients),
                SendStatus = EscalationSendStatus.Pending
            };

            var (escalationEvent, _) = await escalationEvents.GetOrCreateAsync(seed, ct);
            if (escalationEvent.AttemptCount == 0)
            {
                await _domainEvents.PublishAsync(
                    new TicketSlaEscalationTriggeredDomainEvent(
                        ticket.Id,
                        rule.Metric,
                        rule.TriggerPercent,
                        recipients.Count,
                        escalationEvent.AttemptCount,
                        null,
                        nowUtc,
                        GetCorrelationId()),
                    ct);
            }

            if (escalationEvent.SendStatus == EscalationSendStatus.Sent)
            {
                continue;
            }

            if (escalationEvent.AttemptCount >= MaxAttempts)
            {
                continue;
            }

            if (escalationEvent.LastAttemptAt.HasValue &&
                nowUtc - escalationEvent.LastAttemptAt.Value < RetryBackoff)
            {
                continue;
            }

            escalationEvent.PolicyId = policy.Id;
            escalationEvent.RecipientsCsv = string.Join(",", recipients);
            escalationEvent.LastAttemptAt = nowUtc;
            escalationEvent.AttemptCount++;

            try
            {
                var email = emailTemplate.BuildWarning(ticket, snapshot, rule, recipients);
                await emailSender.SendAsync(email, ct);
                escalationEvent.SendStatus = EscalationSendStatus.Sent;
                escalationEvent.SentAt = nowUtc;
                escalationEvent.LastError = null;
                await escalationEvents.UpdateAsync(escalationEvent, ct);
                await _domainEvents.PublishAsync(
                    new TicketSlaEscalationSentDomainEvent(
                        ticket.Id,
                        rule.Metric,
                        rule.TriggerPercent,
                        recipients.Count,
                        escalationEvent.AttemptCount,
                        null,
                        nowUtc,
                        GetCorrelationId()),
                    ct);
            }
            catch (Exception ex)
            {
                escalationEvent.SendStatus = EscalationSendStatus.Failed;
                escalationEvent.LastError = ex.Message;
                await escalationEvents.UpdateAsync(escalationEvent, ct);
                await _domainEvents.PublishAsync(
                    new TicketSlaEscalationFailedDomainEvent(
                        ticket.Id,
                        rule.Metric,
                        rule.TriggerPercent,
                        recipients.Count,
                        escalationEvent.AttemptCount,
                        ex.Message,
                        nowUtc,
                        GetCorrelationId()),
                    ct);
                logger.LogError(
                    ex,
                    "Failed to send SLA escalation email. TicketId={TicketId} Metric={Metric} TriggerPercent={TriggerPercent}",
                    ticket.Id,
                    rule.Metric,
                    rule.TriggerPercent);
            }
        }
    }

    private async Task MarkNoRecipientsFailureAsync(
        Ticket ticket,
        SlaPolicy policy,
        SlaEscalationRule rule,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var seed = new TicketSlaEscalationEvent
        {
            TicketId = ticket.Id,
            Metric = rule.Metric,
            TriggerPercent = rule.TriggerPercent,
            PolicyId = policy.Id,
            RecipientsCsv = string.Empty,
            SendStatus = EscalationSendStatus.Pending
        };

        var (eventRow, _) = await escalationEvents.GetOrCreateAsync(seed, ct);
        eventRow.PolicyId = policy.Id;
        eventRow.RecipientsCsv = string.Empty;
        eventRow.LastAttemptAt = nowUtc;
        eventRow.AttemptCount++;
        eventRow.SendStatus = EscalationSendStatus.Failed;
        eventRow.LastError = "No recipients resolved";
        await escalationEvents.UpdateAsync(eventRow, ct);
        await _domainEvents.PublishAsync(
            new TicketSlaEscalationFailedDomainEvent(
                ticket.Id,
                rule.Metric,
                rule.TriggerPercent,
                0,
                eventRow.AttemptCount,
                eventRow.LastError,
                nowUtc,
                GetCorrelationId()),
            ct);
    }

    private static TicketType? ResolveTicketType(Ticket ticket)
    {
        return ticket switch
        {
            Incident => TicketType.Incident,
            Request => TicketType.Request,
            Change => TicketType.Change,
            _ => null
        };
    }

    private sealed class NullWorkingCalendarResolver : IWorkingCalendarResolver
    {
        public Task<WorkingCalendar?> ResolveAsync(string? tenantId)
        {
            _ = tenantId;
            return Task.FromResult<WorkingCalendar?>(null);
        }
    }

    private sealed class BasicRecipientResolver : IRecipientResolver
    {
        public Task<List<string>> ResolveEmailsAsync(string tenantId, List<RecipientTarget> targets, CancellationToken ct = default)
        {
            _ = tenantId;
            _ = ct;
            var emails = targets
                .Where(x => x.Type == RecipientTargetType.Email && !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => x.Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult(emails);
        }
    }

    private string GetCorrelationId()
    {
        return _correlationContext?.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }
}
