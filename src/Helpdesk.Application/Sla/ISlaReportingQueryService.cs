using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Sla;

namespace Helpdesk.Application.Sla;

public interface ISlaReportingQueryService
{
    Task<SlaComplianceSummaryDto> GetComplianceSummaryAsync(SlaComplianceQuery query, CancellationToken ct);
    Task<PagedResult<SlaTicketRowDto>> GetBreachedTicketsAsync(SlaTicketListQuery query, CancellationToken ct);
    Task<PagedResult<SlaTicketRowDto>> GetNearBreachTicketsAsync(SlaNearBreachQuery query, CancellationToken ct);
    Task<PagedResult<SlaCompletedRowDto>> GetCompletedTicketsAsync(SlaCompletedQuery query, CancellationToken ct);
}
