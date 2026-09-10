namespace Helpdesk.Shared.DTOs.AI;

public class AiModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? ExtraHeadersJson { get; set; }
}
