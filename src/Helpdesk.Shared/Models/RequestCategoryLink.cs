namespace Helpdesk.Shared.Models;

public class RequestCategoryLink
{
    public string RequestId { get; set; } = string.Empty;
    public Guid TicketCategoryId { get; set; }

    public Request Request { get; set; } = default!;
    public TicketCategory TicketCategory { get; set; } = default!;
}
