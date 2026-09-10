namespace Helpdesk.Application.Services.AI;

public sealed record AiOperationAuditEntry(
    string OperationName,
    string? OrganizationId,
    string ProviderName,
    string ModelId,
    string? CorrelationId = null,
    string? SubjectId = null,
    string? Notes = null);

public interface IAiOperationAuditService
{
    Task RecordAsync(AiOperationAuditEntry entry, CancellationToken token);
}
