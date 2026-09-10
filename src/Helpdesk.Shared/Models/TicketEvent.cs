using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class TicketEvent
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string TicketId { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.UtcNow;
}
