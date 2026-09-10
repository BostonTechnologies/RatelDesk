namespace Helpdesk.Application.RequestTasks;

public static class AutomationTaskStatuses
{
    public const string SubmitFailedManualRetry = "SubmitFailedManualRetry";

    public static bool RequiresManualRetry(string? status)
        => string.Equals(status, SubmitFailedManualRetry, StringComparison.OrdinalIgnoreCase);
}
