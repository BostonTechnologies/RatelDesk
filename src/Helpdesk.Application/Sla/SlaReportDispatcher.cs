using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Application.Events;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Sla;

public class SlaReportDispatcher(
    IRepository<SlaReportSubscription> subscriptions,
    ISlaReportGenerator reportGenerator,
    IRecipientResolver recipientResolver,
    ISlaReportSendEventRepository sendEvents,
    IEmailSender emailSender,
    ILogger<SlaReportDispatcher> logger,
    IDomainEventPublisher? domainEvents = null,
    ICorrelationContext? correlationContext = null) : ISlaReportDispatcher
{
    private readonly IRepository<SlaReportSubscription> _subscriptions = subscriptions;
    private readonly ISlaReportGenerator _reportGenerator = reportGenerator;
    private readonly IRecipientResolver _recipientResolver = recipientResolver;
    private readonly ISlaReportSendEventRepository _sendEvents = sendEvents;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly ILogger<SlaReportDispatcher> _logger = logger;
    private readonly IDomainEventPublisher _domainEvents = domainEvents ?? NoopDomainEventPublisher.Instance;
    private readonly ICorrelationContext? _correlationContext = correlationContext;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var all = await _subscriptions.GetAllAsync();
        var active = all.Where(x => x.IsActive).ToList();

        foreach (var subscription in active)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var due = TryGetDueWindow(subscription, nowUtc);
            if (due is null)
            {
                continue;
            }

            var seed = new SlaReportSendEvent
            {
                SubscriptionId = subscription.Id,
                PeriodStartUtc = due.Value.PeriodStartUtc,
                PeriodEndUtc = due.Value.PeriodEndUtc,
                SentAtUtc = nowUtc,
                Status = ReportSendStatus.Pending
            };

            var (sendEvent, created) = await _sendEvents.GetOrCreateAsync(seed, ct);
            if (!created && sendEvent.Status == ReportSendStatus.Sent)
            {
                continue;
            }

            sendEvent.AttemptCount++;
            sendEvent.SentAtUtc = nowUtc;

            try
            {
                var resolvedRecipients = await _recipientResolver.ResolveEmailsAsync(subscription.TenantId, subscription.Targets, ct);
                if (resolvedRecipients.Count == 0)
                {
                    sendEvent.Status = ReportSendStatus.Failed;
                    sendEvent.LastError = "No recipients resolved";
                    await _sendEvents.UpdateAsync(sendEvent, ct);
                    await _domainEvents.PublishAsync(
                        new SlaReportSendFailedDomainEvent(
                            subscription.Id,
                            subscription.TenantId,
                            due.Value.PeriodStartUtc,
                            due.Value.PeriodEndUtc,
                            0,
                            sendEvent.AttemptCount,
                            sendEvent.LastError,
                            nowUtc,
                            GetCorrelationId()),
                        ct);
                    continue;
                }

                var report = await _reportGenerator.GenerateAsync(new SlaComplianceQuery
                {
                    TenantId = subscription.TenantId,
                    TicketType = subscription.TicketType,
                    FromUtc = due.Value.PeriodStartUtc,
                    ToUtc = due.Value.PeriodEndUtc
                }, ct);
                await _domainEvents.PublishAsync(
                    new SlaReportGeneratedDomainEvent(
                        subscription.Id,
                        subscription.TenantId,
                        due.Value.PeriodStartUtc,
                        due.Value.PeriodEndUtc,
                        resolvedRecipients.Count,
                        sendEvent.AttemptCount,
                        nowUtc,
                        GetCorrelationId()),
                    ct);

                var message = new EmailMessage
                {
                    To = resolvedRecipients,
                    Subject = report.Subject,
                    HtmlBody = report.HtmlBody
                };

                if (subscription.IncludeCsvAttachment && report.CsvBytes is not null)
                {
                    message.Attachments.Add(new EmailAttachment
                    {
                        FileName = report.CsvFileName,
                        ContentType = "text/csv",
                        ContentBytes = report.CsvBytes
                    });
                }

                if (subscription.IncludeExcelAttachment && report.ExcelBytes is not null)
                {
                    message.Attachments.Add(new EmailAttachment
                    {
                        FileName = report.ExcelFileName,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        ContentBytes = report.ExcelBytes
                    });
                }

                await _emailSender.SendAsync(message, ct);

                sendEvent.Status = ReportSendStatus.Sent;
                sendEvent.LastError = null;
                await _sendEvents.UpdateAsync(sendEvent, ct);
                await _domainEvents.PublishAsync(
                    new SlaReportSentDomainEvent(
                        subscription.Id,
                        subscription.TenantId,
                        due.Value.PeriodStartUtc,
                        due.Value.PeriodEndUtc,
                        resolvedRecipients.Count,
                        sendEvent.AttemptCount,
                        nowUtc,
                        GetCorrelationId()),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SLA report subscription {SubscriptionId}", subscription.Id);
                sendEvent.Status = ReportSendStatus.Failed;
                sendEvent.LastError = ex.Message;
                await _sendEvents.UpdateAsync(sendEvent, ct);
                await _domainEvents.PublishAsync(
                    new SlaReportSendFailedDomainEvent(
                        subscription.Id,
                        subscription.TenantId,
                        due.Value.PeriodStartUtc,
                        due.Value.PeriodEndUtc,
                        0,
                        sendEvent.AttemptCount,
                        ex.Message,
                        nowUtc,
                        GetCorrelationId()),
                    ct);
            }
        }
    }

    private static DueWindow? TryGetDueWindow(SlaReportSubscription subscription, DateTimeOffset nowUtc)
    {
        var timeZone = ResolveTimeZone(subscription.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var localDate = DateOnly.FromDateTime(nowLocal.Date);
        var scheduledLocal = localDate.ToDateTime(TimeOnly.MinValue).Add(subscription.SendTimeLocal);
        var windowStart = new DateTimeOffset(scheduledLocal, nowLocal.Offset);
        var windowEnd = windowStart.AddHours(1);

        if (nowLocal < windowStart || nowLocal >= windowEnd)
        {
            return null;
        }

        if (subscription.Frequency == ReportFrequency.Weekly)
        {
            var weeklyDay = subscription.WeeklyDay ?? DayOfWeek.Monday;
            if (nowLocal.DayOfWeek != weeklyDay)
            {
                return null;
            }
        }
        else if (subscription.Frequency == ReportFrequency.Monthly && nowLocal.Day != 1)
        {
            return null;
        }

        var periodEndUtc = TimeZoneInfo.ConvertTime(windowStart, TimeZoneInfo.Utc);
        var periodStartUtc = periodEndUtc.AddDays(-Math.Max(1, subscription.LookbackDays));
        return new DueWindow(periodStartUtc, periodEndUtc);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private readonly record struct DueWindow(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);

    private string GetCorrelationId()
    {
        return _correlationContext?.GetCorrelationId() ?? $"corr-{Guid.NewGuid():N}";
    }
}
