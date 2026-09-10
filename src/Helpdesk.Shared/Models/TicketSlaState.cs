using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class TicketSlaState
{
    public string TicketId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ResponseDueAt { get; set; }
    public DateTimeOffset ResolutionDueAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public TimeSpan AccumulatedPauseDuration { get; set; }
    public long AccumulatedPauseWorkingSeconds { get; set; }
    public string? PauseReason { get; set; }
    public string? PausedByUserId { get; set; }
    public DateTimeOffset? ResumeAt { get; set; }
    public DateTimeOffset? LastResumedAt { get; set; }
    public bool IsBusinessHours { get; set; }
    public string? CalendarId { get; set; }
    public SlaStatus Status { get; set; }
    public bool ResponseBreached { get; set; }
    public bool ResolutionBreached { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool CompletedWithinResponseSla { get; set; }
    public bool CompletedWithinResolutionSla { get; set; }
}
