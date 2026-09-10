using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.Models;

public class EmailTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty; // Unique identifier like "NewTicketConfirmation"

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;

    public int? LayoutId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; set; }

    public bool IsSystem { get; set; }

    public int Version { get; set; } = 1;
}
