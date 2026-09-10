namespace Helpdesk.Shared.DTOs.AI;

public class AiChatRequestDto
{
    public string Prompt { get; set; } = string.Empty;
    public string? Model { get; set; }
}

