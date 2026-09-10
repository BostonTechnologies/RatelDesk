namespace Helpdesk.Shared.DTOs.AI;

public class AiProviderTestResultDto
{
    public bool Success { get; set; }
    public string? AttemptedUrl { get; set; }
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public string? ErrorBody { get; set; }
}
