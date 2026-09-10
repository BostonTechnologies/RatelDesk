using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Shared.DTOs.Worklog;

public class CreateWorkLogDto
{
    [Required(ErrorMessage = "Notes are required to create a worklog entry.")]
    public string? Notes { get; set; }

    public double Hours { get; set; }

    public bool IsInternalNote { get; set; }
}
