namespace Helpdesk.Application.Sla;

public interface ITicketSlaQueryRepository
{
    Task<IReadOnlyList<TicketSlaBatchRow>> GetBatchAsync(int take, string? cursor, CancellationToken ct);
}
