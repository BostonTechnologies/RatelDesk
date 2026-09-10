using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.Models;

public class EmailLayout
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public int? TenantId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; set; }

    public int Version { get; set; } = 1;
}
