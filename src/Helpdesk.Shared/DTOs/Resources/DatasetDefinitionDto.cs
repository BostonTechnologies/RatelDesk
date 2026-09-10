using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Resources;

public class DatasetDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DatasetSourceType SourceType { get; set; }
    public string KeyColumn { get; set; } = string.Empty;
    public string DisplayColumn { get; set; } = string.Empty;
    public List<string> SearchColumns { get; set; } = new();
    public bool IsBuiltIn { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<DatasetColumnDto> Columns { get; set; } = new();
}
