using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Sla;

public class TicketSlaDto
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ResponseDueAt { get; set; }
    public DateTimeOffset ResolutionDueAt { get; set; }
    public SlaStatus Status { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public DateTimeOffset? ResumeAt { get; set; }
    public string? PauseReason { get; set; }
    public bool ResponseBreached { get; set; }
    public bool ResolutionBreached { get; set; }
    public long ResponseRemainingSeconds { get; set; }
    public long ResolutionRemainingSeconds { get; set; }
    public int ResponsePercentUsed { get; set; }
    public int ResolutionPercentUsed { get; set; }
}
