namespace Helpdesk.Shared.Models;

public class AiOperationAuditRecord
{
    public Guid Id { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? SubjectId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
