using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class Service
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ParentServiceId { get; set; }
    public List<string> AllowedCustomerIds { get; set; } = new();

    public List<string> AllowedOrganizationIds { get; set; } = new();
}

