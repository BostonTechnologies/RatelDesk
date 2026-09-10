using Dodo.Primitives;
using System.Text.Json;

namespace Helpdesk.Shared.Models;

public class RequestForm
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string Description { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public JsonDocument JsonSchema { get; set; } = JsonDocument.Parse("{}");
    public string? OrganizationId { get; set; } = string.Empty;
    public List<string> AllowedOrganizationIds { get; set; } = new();
    public RequestFormReleaseStatus ReleaseStatus { get; set; } = RequestFormReleaseStatus.InTesting;
}
