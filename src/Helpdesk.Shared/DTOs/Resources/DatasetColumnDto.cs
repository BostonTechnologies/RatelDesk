using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Resources;

public class DatasetColumnDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DatasetColumnDataType DataType { get; set; }
    public bool IsKey { get; set; }
    public bool IsDisplay { get; set; }
    public bool IsSearchable { get; set; }
    public int SortOrder { get; set; }
}
