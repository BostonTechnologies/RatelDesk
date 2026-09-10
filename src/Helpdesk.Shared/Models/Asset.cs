using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class Asset
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string OrganizationId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
