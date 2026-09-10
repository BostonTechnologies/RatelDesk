using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class BlockedEntity
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string? Email { get; set; }
    public string? Domain { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime BlockedOn { get; set; } = DateTime.UtcNow;
}
