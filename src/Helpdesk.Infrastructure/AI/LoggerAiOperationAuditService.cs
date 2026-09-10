using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.AI;

public sealed class LoggerAiOperationAuditService(
    HelpdeskDbContext db,
    ILogger<LoggerAiOperationAuditService> logger) : IAiOperationAuditService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly ILogger<LoggerAiOperationAuditService> _logger = logger;

    public async Task RecordAsync(AiOperationAuditEntry entry, CancellationToken token)
    {
        _db.Add(new Shared.Models.AiOperationAuditRecord
        {
            OperationName = entry.OperationName,
            OrganizationId = entry.OrganizationId,
            ProviderName = entry.ProviderName,
            ModelId = entry.ModelId,
            CorrelationId = entry.CorrelationId,
            SubjectId = entry.SubjectId,
            Notes = entry.Notes
        });

        await _db.SaveChangesAsync(token);

        _logger.LogInformation(
            "AI operation {Operation} org={OrganizationId} provider={Provider} model={Model} correlation={CorrelationId} subject={SubjectId}",
            entry.OperationName,
            entry.OrganizationId,
            entry.ProviderName,
            entry.ModelId,
            entry.CorrelationId,
            entry.SubjectId);
    }
}
