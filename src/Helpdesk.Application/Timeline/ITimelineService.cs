namespace Helpdesk.Application.Timeline;

public interface ITimelineService
{
    Task RetryEmailAsync(
        Guid timelineEventId,
        string userId,
        CancellationToken ct);

    Task<int> RetryAllFailedEmailsAsync(
        string userId,
        CancellationToken ct);

    Task<int> GetPendingEmailCountAsync(
        string userId,
        CancellationToken ct);
}
