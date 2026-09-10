using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class SupportGroup
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string OwningOrganizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
