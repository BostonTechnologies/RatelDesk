using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.DTOs.Category;

public class CreateTicketCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TicketCategoryType Type { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
