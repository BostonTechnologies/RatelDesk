using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class SlaPolicy
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SlaScopeType ScopeType { get; set; } = SlaScopeType.SystemDefault;
    public string? TenantId { get; set; }
    public TicketType AppliesTo { get; set; } = TicketType.Incident;
    public int? Priority { get; set; }
    public string? ServiceId { get; set; }
    public int MatchRank { get; set; }
    public int ResponseTimeHours { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int? AutoResumeAfterHours { get; set; }
    public bool IsActive { get; set; } = true;
    public List<SlaEscalationRule> Escalations { get; set; } = new();
}
