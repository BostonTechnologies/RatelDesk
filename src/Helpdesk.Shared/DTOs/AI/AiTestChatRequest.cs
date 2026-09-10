namespace Helpdesk.Shared.DTOs.AI;

public class AiTestChatRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? Model { get; set; }
}
