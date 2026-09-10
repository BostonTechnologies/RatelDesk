using Helpdesk.Shared.DTOs.Sla;

namespace Helpdesk.Application.Sla;

public interface ISlaReportGenerator
{
    Task<GeneratedReport> GenerateAsync(SlaComplianceQuery query, CancellationToken ct = default);
}
