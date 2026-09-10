using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class AiProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraHeadersJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<AiModel> Models { get; set; } = new List<AiModel>();
}
