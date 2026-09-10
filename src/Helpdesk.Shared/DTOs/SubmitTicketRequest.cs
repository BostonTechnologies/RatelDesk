using Helpdesk.Shared.Models;

namespace Helpdesk.Shared.DTOs;

public class SubmitTicketRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Low;
    public Guid CaptchaId { get; set; }
    public string CaptchaAnswer { get; set; } = string.Empty;
}
