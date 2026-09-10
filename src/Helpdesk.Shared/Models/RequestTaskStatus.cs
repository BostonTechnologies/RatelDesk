namespace Helpdesk.Shared.Models;

public enum RequestTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5,
    PendingApproval = 6,
    Cancelled = 7
}
