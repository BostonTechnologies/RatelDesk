namespace Helpdesk.Shared.DTOs.Change;

public class ChangeTemplateValidationDto
{
    public bool IsComplete { get; set; }
    public List<string> Errors { get; set; } = new();
}
