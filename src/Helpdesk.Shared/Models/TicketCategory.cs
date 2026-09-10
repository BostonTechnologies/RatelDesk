using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class TicketCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public TicketCategoryType Type { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? ParentCategoryId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystem { get; set; } = false;

    public DateTime CreatedUtc { get; set; }

    public DateTime? UpdatedUtc { get; set; }
}
