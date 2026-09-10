using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class ErrorLog
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? User { get; set; }
    public string? Page { get; set; }
}
