using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class DatasetIngestCredential
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
