namespace Helpdesk.Shared.DTOs.AI;

using Helpdesk.Shared.Enums;

public class AiProviderSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? DefaultModel { get; set; }
    public IEnumerable<AiModelDto> Models { get; set; } = new List<AiModelDto>();
}
