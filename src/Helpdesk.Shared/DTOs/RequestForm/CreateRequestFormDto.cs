using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs.RequestForm;

public class CreateRequestFormDto
{
    [Required]
    public string ServiceId { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    // NEW: optional, matches model
    public string? Description { get; set; }

    [Required]
    public string JsonSchema { get; set; } = string.Empty;

    public string? OrganizationId { get; set; }
    public List<string> AllowedOrganizationIds { get; set; } = new();
    public RequestFormReleaseStatus ReleaseStatus { get; set; } = RequestFormReleaseStatus.InTesting;
}
