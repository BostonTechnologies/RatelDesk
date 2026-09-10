namespace Helpdesk.Shared.DTOs.Service;

using System.ComponentModel.DataAnnotations;

public class CreateServiceDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ParentServiceId { get; set; }
    public string? ParentId { get; set; }
    public List<string>? AllowedCustomerIds { get; set; }
    public List<string>? AllowedOrganizationIds { get; set; }

}

