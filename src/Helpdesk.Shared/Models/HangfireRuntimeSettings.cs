namespace Helpdesk.Shared.Models;

public class HangfireRuntimeSettings
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}
