namespace Helpdesk.Application.Services.AI;

public sealed record AiProviderConnectionTestResult(
    bool Success,
    string? AttemptedUrl = null,
    int? StatusCode = null,
    string? Message = null,
    string? ErrorBody = null);
