namespace Helpdesk.Shared.Models;

public class AiModel
{
    public Guid Id { get; set; }
    public Guid AiProviderId { get; set; }
    public AiProvider Provider { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? ExtraHeadersJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
