namespace Helpdesk.Shared.DTOs.Service;

public class ServiceDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = string.Empty;

    // Parent relationship
    public string? ParentServiceId { get; set; }

    // Who can see/use this service
    public List<string> AllowedCustomerIds { get; set; } = new();

    public List<string> AllowedOrganizationIds { get; set; } = new();

    // NEW: computed on the server; 0 = root
    public int Depth { get; set; } = 0;
}

