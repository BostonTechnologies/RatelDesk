using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Resources;

public class UpdateDatasetDefinitionDto
{
    public string? Name { get; set; }
    public DatasetSourceType? SourceType { get; set; }
    public string? KeyColumn { get; set; }
    public string? DisplayColumn { get; set; }
    public List<string>? SearchColumns { get; set; }
    public bool? IsActive { get; set; }
    public List<DatasetColumnDto>? Columns { get; set; }
}
