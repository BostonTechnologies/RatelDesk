using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SlaEscalationRule
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string PolicyId { get; set; } = string.Empty;
    public SlaMetricType Metric { get; set; }
    public int TriggerPercent { get; set; }
    // Deprecated: kept for backward compatibility and migration safety.
    public List<string> Recipients { get; set; } = new();
    public List<RecipientTarget> Targets { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}
