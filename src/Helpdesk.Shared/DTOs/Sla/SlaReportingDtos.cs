using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Sla;

public class SlaComplianceSummaryDto
{
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int CompletedTotal { get; set; }
    public int CompletedWithinResolutionSla { get; set; }
    public int CompletedBreachedResolutionSla { get; set; }
    public int CompletedWithinResponseSla { get; set; }
    public int CompletedBreachedResponseSla { get; set; }
    public double ResolutionCompliancePercent { get; set; }
    public double ResponseCompliancePercent { get; set; }
    public List<SlaComplianceBucketDto> Buckets { get; set; } = new();
}

public class SlaComplianceBucketDto
{
    public string Label { get; set; } = string.Empty;
    public int CompletedTotal { get; set; }
    public int CompletedWithinResolutionSla { get; set; }
}

public class SlaTicketRowDto
{
    public string TicketId { get; set; } = string.Empty;
    public string? TicketNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public TicketType TicketType { get; set; }
    public SlaStatus SlaStatus { get; set; }
    public bool ResponseBreached { get; set; }
    public bool ResolutionBreached { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ResponseDueAt { get; set; }
    public DateTimeOffset ResolutionDueAt { get; set; }
    public long ResponseRemainingSeconds { get; set; }
    public long ResolutionRemainingSeconds { get; set; }
    public int ResponsePercentUsed { get; set; }
    public int ResolutionPercentUsed { get; set; }
}

public class SlaCompletedRowDto
{
    public string TicketId { get; set; } = string.Empty;
    public string? TicketNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public TicketType TicketType { get; set; }
    public string? ServiceId { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public bool WithinResponseSla { get; set; }
    public bool WithinResolutionSla { get; set; }
}
