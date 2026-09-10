namespace Helpdesk.Shared.DTOs.RequestForm;

using Helpdesk.Shared.Models;

public class RequestFormDto
{
    public string Id { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = string.Empty;

    public string? OrganizationId { get; set; }
    public List<string> AllowedOrganizationIds { get; set; } = new();
    public RequestFormReleaseStatus ReleaseStatus { get; set; } = RequestFormReleaseStatus.InTesting;
}
