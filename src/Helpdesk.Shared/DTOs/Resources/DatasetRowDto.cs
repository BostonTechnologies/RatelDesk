using System.Text.Json.Nodes;

namespace Helpdesk.Shared.DTOs.Resources;

public class DatasetRowDto
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string ExternalKey { get; set; } = string.Empty;
    public JsonObject Data { get; set; } = new();
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public DateTimeOffset LastIngestedAtUtc { get; set; }
}
