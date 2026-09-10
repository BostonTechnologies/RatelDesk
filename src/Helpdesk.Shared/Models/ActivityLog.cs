using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class ActivityLog
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string? TicketId { get; set; }
    public string? RelatedEntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
