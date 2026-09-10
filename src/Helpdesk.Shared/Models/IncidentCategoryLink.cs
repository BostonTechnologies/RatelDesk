namespace Helpdesk.Shared.Models;

public class IncidentCategoryLink
{
    public string IncidentId { get; set; } = string.Empty;
    public Guid TicketCategoryId { get; set; }

    public Incident Incident { get; set; } = default!;
    public TicketCategory TicketCategory { get; set; } = default!;
}
