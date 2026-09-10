using System.Text.Json;
using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class DatasetRow
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string DatasetId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string ExternalKey { get; set; } = string.Empty;
    public JsonDocument DataJson { get; set; } = JsonDocument.Parse("{}");
    public string SearchText { get; set; } = string.Empty;
    public string RowHash { get; set; } = string.Empty;
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public DateTimeOffset LastIngestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
