using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.Incident;

public class CreateTicketRelationDto
{
    public string TargetTicketId { get; set; } = string.Empty;

    public TicketRelationType RelationType { get; set; }

    public bool CloseSourceTicket { get; set; } = true;

    public string? Note { get; set; }
}

public class TicketRelationDto
{
    public Guid Id { get; set; }

    public string SourceTicketId { get; set; } = string.Empty;

    public string SourceTrackingId { get; set; } = string.Empty;

    public string SourceSubject { get; set; } = string.Empty;

    public TicketState SourceState { get; set; }

    public string TargetTicketId { get; set; } = string.Empty;

    public string TargetTrackingId { get; set; } = string.Empty;

    public string TargetSubject { get; set; } = string.Empty;

    public TicketState TargetState { get; set; }

    public TicketRelationType RelationType { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;

    public string? Note { get; set; }

    public bool IsOutgoing { get; set; }
}

public class TicketRelationCreateResultDto
{
    public TicketRelationDto Relation { get; set; } = new();

    public IncidentDto SourceIncident { get; set; } = new();

    public IncidentDto TargetIncident { get; set; } = new();

    public List<string> AddedParentListeners { get; set; } = new();
}
