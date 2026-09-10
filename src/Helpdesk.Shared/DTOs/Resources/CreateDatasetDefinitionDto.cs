using System.ComponentModel.DataAnnotations;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Resources;

public class CreateDatasetDefinitionDto
{
    [Required]
    public string OrganizationId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public DatasetSourceType SourceType { get; set; } = DatasetSourceType.Custom;
    public string KeyColumn { get; set; } = string.Empty;
    public string DisplayColumn { get; set; } = string.Empty;
    public List<string> SearchColumns { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public List<DatasetColumnDto> Columns { get; set; } = new();
}
