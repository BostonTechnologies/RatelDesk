using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class DatasetColumn
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DatasetColumnDataType DataType { get; set; } = DatasetColumnDataType.Text;
    public bool IsKey { get; set; }
    public bool IsDisplay { get; set; }
    public bool IsSearchable { get; set; }
    public int SortOrder { get; set; }
}
