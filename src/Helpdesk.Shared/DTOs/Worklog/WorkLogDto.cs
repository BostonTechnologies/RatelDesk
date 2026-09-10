namespace Helpdesk.Shared.DTOs.Worklog;

// This DTO is for *displaying* a worklog.
public class WorkLogDto
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public double Hours { get; set; }
    public bool IsInternalNote { get; set; }
    public string? NotesHtml { get; set; }
    public string? NotesText { get; set; }
    public string? Notes
    {
        get => NotesText;
        set => NotesText = value;
    }
    public DateTime LoggedAt { get; set; }
    public string? TechnicianId { get; set; }

    // It's very useful to include the user's name for display purposes.
    // We will populate this on the backend.
    public string? TechnicianName { get; set; }
}
