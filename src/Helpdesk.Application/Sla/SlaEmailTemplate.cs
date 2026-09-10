using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Application.Sla;

public class SlaEmailTemplate(IConfiguration configuration) : ISlaEmailTemplate
{
    private readonly string _publicWebAppUrl = (configuration["PublicWebAppUrl"] ?? string.Empty).TrimEnd('/');

    public EmailMessage BuildWarning(Ticket ticket, SlaClockSnapshot snapshot, SlaEscalationRule rule, List<string> recipients)
    {
        var metricLabel = rule.Metric == SlaMetricType.Response ? "Response" : "Resolution";
        var currentPercent = rule.Metric == SlaMetricType.Response
            ? snapshot.ResponsePercentUsed
            : snapshot.ResolutionPercentUsed;
        var remaining = rule.Metric == SlaMetricType.Response
            ? snapshot.ResponseRemaining
            : snapshot.ResolutionRemaining;
        var ticketRef = string.IsNullOrWhiteSpace(ticket.TrackingId) ? ticket.Id : ticket.TrackingId;
        var ticketLink = BuildTicketLink(ticketRef);

        var noteSection = string.IsNullOrWhiteSpace(rule.Note)
            ? string.Empty
            : $"<p><strong>Note:</strong> {System.Net.WebUtility.HtmlEncode(rule.Note)}</p>";

        var html = $"""
            <p>SLA threshold reached for ticket <strong>{System.Net.WebUtility.HtmlEncode(ticketRef)}</strong>.</p>
            <ul>
                <li><strong>Ticket:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Title)}</li>
                <li><strong>Tenant:</strong> {System.Net.WebUtility.HtmlEncode(ticket.OrganizationId ?? "(none)")}</li>
                <li><strong>Metric:</strong> {metricLabel}</li>
                <li><strong>Threshold:</strong> {rule.TriggerPercent}%</li>
                <li><strong>Current:</strong> {currentPercent}%</li>
                <li><strong>Remaining:</strong> {FormatFriendly(remaining)}</li>
            </ul>
            {noteSection}
            <p><a href="{ticketLink}">Open Ticket</a></p>
            """;

        return new EmailMessage
        {
            To = recipients,
            Subject = $"[Helpdesk] SLA Warning {rule.TriggerPercent}% ({metricLabel}) - {ticketRef}",
            HtmlBody = html,
            TextBody = $"SLA warning: {rule.TriggerPercent}% ({metricLabel}) for ticket {ticketRef}. Current={currentPercent}% Remaining={FormatFriendly(remaining)}."
        };
    }

    private string BuildTicketLink(string ticketRef)
    {
        if (string.IsNullOrWhiteSpace(_publicWebAppUrl))
        {
            return "#";
        }

        return $"{_publicWebAppUrl}/tickets/{Uri.EscapeDataString(ticketRef)}";
    }

    private static string FormatFriendly(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "0m";
        }

        var days = (int)span.TotalDays;
        var hours = span.Hours;
        var minutes = span.Minutes;
        if (days > 0)
        {
            return $"{days}d {hours}h";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{Math.Max(1, minutes)}m";
    }
}
