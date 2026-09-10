using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SlaPolicy;

public class SlaPolicyDto
{
    public string Id { get; set; } = string.Empty;
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
    public List<SlaEscalationRuleDto> Escalations { get; set; } = new();
}
