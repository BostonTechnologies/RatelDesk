using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.SupportNotifications;

public class OrganizationSupportCoverageDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerOrganizationId { get; set; } = string.Empty;
    public string ProviderOrganizationId { get; set; } = string.Empty;
    public string SupportGroupId { get; set; } = string.Empty;
    public SupportCoverageRole Role { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public class CreateOrganizationSupportCoverageDto
{
    [Required]
    public string CustomerOrganizationId { get; set; } = string.Empty;

    [Required]
    public string ProviderOrganizationId { get; set; } = string.Empty;

    [Required]
    public string SupportGroupId { get; set; } = string.Empty;

    public SupportCoverageRole Role { get; set; } = SupportCoverageRole.Primary;
    public bool IsEnabled { get; set; } = true;
}

public class UpdateOrganizationSupportCoverageDto
{
    [Required]
    public string CustomerOrganizationId { get; set; } = string.Empty;

    [Required]
    public string ProviderOrganizationId { get; set; } = string.Empty;

    [Required]
    public string SupportGroupId { get; set; } = string.Empty;

    public SupportCoverageRole Role { get; set; } = SupportCoverageRole.Primary;
    public bool IsEnabled { get; set; } = true;
}
