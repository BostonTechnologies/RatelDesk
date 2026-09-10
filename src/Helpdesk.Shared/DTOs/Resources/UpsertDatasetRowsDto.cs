using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace Helpdesk.Shared.DTOs.Resources;

public class UpsertDatasetRowsDto
{
    public bool DeleteMissing { get; set; }
    public List<UpsertDatasetRowDto> Rows { get; set; } = new();
}

public class UpsertDatasetRowDto
{
    [Required]
    public string ExternalKey { get; set; } = string.Empty;
    public JsonObject Data { get; set; } = new();
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
}
