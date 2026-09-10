using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Sla;

public class SlaComplianceQuery
{
    public string? TenantId { get; set; }
    public TicketType? TicketType { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
}

public class SlaTicketListQuery
{
    public string? TenantId { get; set; }
    public TicketType? TicketType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class SlaNearBreachQuery : SlaTicketListQuery
{
    public int ThresholdPercent { get; set; } = 80;
    public SlaMetricType Metric { get; set; } = SlaMetricType.Resolution;
}

public class SlaCompletedQuery : SlaTicketListQuery
{
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public bool? WithinResolutionSla { get; set; }
}
