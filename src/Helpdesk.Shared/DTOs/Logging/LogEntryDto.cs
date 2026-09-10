namespace Helpdesk.Shared.DTOs.Logging;

public class LogEntryDto
{
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? User { get; set; }
    public string? Page { get; set; }
}
