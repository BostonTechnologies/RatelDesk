namespace Helpdesk.Shared.Models;

public class ChangeCategoryLink
{
    public string ChangeId { get; set; } = string.Empty;
    public Guid TicketCategoryId { get; set; }

    public Change Change { get; set; } = default!;
    public TicketCategory TicketCategory { get; set; } = default!;
}
