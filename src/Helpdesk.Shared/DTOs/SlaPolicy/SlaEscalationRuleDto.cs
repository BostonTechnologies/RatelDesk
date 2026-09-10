using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SlaPolicy;

public class SlaEscalationRuleDto
{
    public string Id { get; set; } = string.Empty;
    public SlaMetricType Metric { get; set; }
    public int TriggerPercent { get; set; }
    // Deprecated: kept for backward compatibility with existing clients.
    public List<string> Recipients { get; set; } = new();
    public List<RecipientTargetDto> Targets { get; set; } = new();
    public bool IsActive { get; set; }
    public string? Note { get; set; }
}
