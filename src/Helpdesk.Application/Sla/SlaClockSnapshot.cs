using Helpdesk.Shared.Enums;

namespace Helpdesk.Application.Sla;

public record SlaClockSnapshot(
    SlaStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ResponseDueAt,
    DateTimeOffset ResolutionDueAt,
    TimeSpan AccumulatedPause,
    DateTimeOffset? PausedAt,
    DateTimeOffset? ResumeAt,
    string? PauseReason,
    TimeSpan ResponseRemaining,
    TimeSpan ResolutionRemaining,
    int ResponsePercentUsed,
    int ResolutionPercentUsed,
    bool ResponseBreached,
    bool ResolutionBreached
);
