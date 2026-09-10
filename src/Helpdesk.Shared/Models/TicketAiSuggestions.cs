namespace Helpdesk.Shared.Models;

public class TicketAiSuggestions
{
    public string TicketId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ItemsJson { get; set; } = "[]";
}

