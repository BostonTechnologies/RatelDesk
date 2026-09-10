using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface ITicketUnderstandingService
{
    Task<string> UnderstandAsync(Ticket ticket, CancellationToken token);
    Task<TicketKnowledgeSuggestion?> AnalyzeTicketAsync(Ticket ticket, CancellationToken token);
}

