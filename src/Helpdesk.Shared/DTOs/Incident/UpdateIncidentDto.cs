namespace Helpdesk.Shared.DTOs.Incident;

using Helpdesk.Shared.Models;

public class UpdateIncidentDto
{
    public TicketState? State { get; set; }
    public TicketPriority? Priority { get; set; }
    public List<string>? CcRecipients { get; set; }
    public string? AssignedToId { get; set; }
    public List<Guid>? CategoryIds { get; set; }
}
