using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class SupportGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string OwningOrganizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class CreateSupportGroupDto
{
    [Required]
    public string OwningOrganizationId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdateSupportGroupDto
{
    [Required]
    public string OwningOrganizationId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}
