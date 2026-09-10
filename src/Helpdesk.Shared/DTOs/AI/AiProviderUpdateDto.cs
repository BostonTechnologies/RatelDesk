namespace Helpdesk.Shared.DTOs.AI;

using Helpdesk.Shared.Enums;

public class AiProviderUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraHeadersJson { get; set; }
    public string? ApiKey { get; set; }
}
