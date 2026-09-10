using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Sla;

public sealed record TicketSlaBatchRow(
    string Cursor,
    string TicketId,
    string? TenantId,
    TicketType TicketType,
    string? TicketNumber,
    string Title,
    TicketPriority Priority,
    string? ServiceId,
    bool IsClosed,
    TicketSlaState SlaState
)
{
    public TicketSlaBatchRow(
        string Cursor,
        string TicketId,
        string? TenantId,
        TicketType TicketType,
        string? TicketNumber,
        string Title,
        bool IsClosed,
        TicketSlaState SlaState)
        : this(Cursor, TicketId, TenantId, TicketType, TicketNumber, Title, TicketPriority.Low, null, IsClosed, SlaState)
    {
    }
}
