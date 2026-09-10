using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class TicketRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceTicketId { get; set; } = string.Empty;

    public string TargetTicketId { get; set; } = string.Empty;

    public TicketRelationType RelationType { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CreatedByUserName { get; set; } = string.Empty;

    public string? Note { get; set; }
}
