using System.Globalization;
using System.Text;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Application.Sla;

public class SlaReportGenerator(ISlaReportingQueryService reporting) : ISlaReportGenerator
{
    private readonly ISlaReportingQueryService _reporting = reporting;

    public async Task<GeneratedReport> GenerateAsync(SlaComplianceQuery query, CancellationToken ct = default)
    {
        var compliance = await _reporting.GetComplianceSummaryAsync(query, ct);
        var breached = await _reporting.GetBreachedTicketsAsync(new SlaTicketListQuery
        {
            TenantId = query.TenantId,
            TicketType = query.TicketType,
            Page = 1,
            PageSize = 10
        }, ct);
        var nearBreach = await _reporting.GetNearBreachTicketsAsync(new SlaNearBreachQuery
        {
            TenantId = query.TenantId,
            TicketType = query.TicketType,
            Metric = SlaMetricType.Resolution,
            ThresholdPercent = 80,
            Page = 1,
            PageSize = 10
        }, ct);
        var completed = await _reporting.GetCompletedTicketsAsync(new SlaCompletedQuery
        {
            TenantId = query.TenantId,
            TicketType = query.TicketType,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = 1,
            PageSize = 500
        }, ct);

        var fromLabel = query.FromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toLabel = query.ToUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var subject = $"[Helpdesk] SLA Report {fromLabel} to {toLabel}";

        var html = new StringBuilder();
        html.AppendLine($"<p>SLA report for <strong>{fromLabel}</strong> to <strong>{toLabel}</strong>.</p>");
        html.AppendLine("<h4>Compliance Summary</h4>");
        html.AppendLine("<ul>");
        html.AppendLine($"<li>Completed: {compliance.CompletedTotal}</li>");
        html.AppendLine($"<li>Resolution compliance: {compliance.ResolutionCompliancePercent:0.##}%</li>");
        html.AppendLine($"<li>Response compliance: {compliance.ResponseCompliancePercent:0.##}%</li>");
        html.AppendLine("</ul>");

        html.AppendLine("<h4>Top Breached Tickets</h4>");
        html.AppendLine(RenderTicketList(breached.Items.Select(x => (x.TicketNumber ?? x.TicketId, x.Title))));

        html.AppendLine("<h4>Top Near-Breach Tickets</h4>");
        html.AppendLine(RenderTicketList(nearBreach.Items.Select(x => (x.TicketNumber ?? x.TicketId, x.Title))));

        var report = new GeneratedReport
        {
            Subject = subject,
            HtmlBody = html.ToString(),
            CsvBytes = BuildCompletedCsv(completed.Items),
            CsvFileName = $"sla-report-{fromLabel}-to-{toLabel}.csv",
            ExcelBytes = null,
            ExcelFileName = $"sla-report-{fromLabel}-to-{toLabel}.xlsx"
        };

        return report;
    }

    private static string RenderTicketList(IEnumerable<(string Id, string Title)> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return "<p>None</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<ul>");
        foreach (var row in list)
        {
            html.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(row.Id)} - {System.Net.WebUtility.HtmlEncode(row.Title)}</li>");
        }

        html.AppendLine("</ul>");
        return html.ToString();
    }

    private static byte[] BuildCompletedCsv(IReadOnlyList<SlaCompletedRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TicketNumber,Title,CompletedAt,WithinResolution,WithinResponse,TenantId,ServiceId,Priority");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(row.TicketNumber ?? row.TicketId),
                EscapeCsv(row.Title),
                EscapeCsv(row.CompletedAt.ToString("O", CultureInfo.InvariantCulture)),
                row.WithinResolutionSla ? "true" : "false",
                row.WithinResponseSla ? "true" : "false",
                EscapeCsv(row.TenantId),
                EscapeCsv(row.ServiceId ?? string.Empty),
                row.Priority.ToString(CultureInfo.InvariantCulture)
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
