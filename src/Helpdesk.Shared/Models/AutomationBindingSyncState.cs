namespace Helpdesk.Shared.Models;

public enum AutomationBindingSyncState
{
    InSync = 0,
    Drifted = 1,
    ImportPending = 2,
    Broken = 3
}
