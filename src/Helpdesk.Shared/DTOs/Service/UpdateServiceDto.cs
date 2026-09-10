namespace Helpdesk.Shared.DTOs.Service;

public class UpdateServiceDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public List<string>? AllowedCustomerIds { get; set; }
    public List<string>? AllowedOrganizationIds { get; set; }
}

