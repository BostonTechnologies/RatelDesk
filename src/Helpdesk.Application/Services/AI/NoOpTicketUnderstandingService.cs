using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public class NoOpTicketUnderstandingService : ITicketUnderstandingService
{
    public Task<string> UnderstandAsync(Ticket ticket, CancellationToken token) => Task.FromResult(string.Empty);

    public Task<TicketKnowledgeSuggestion?> AnalyzeTicketAsync(Ticket ticket, CancellationToken token) => Task.FromResult<TicketKnowledgeSuggestion?>(null);
}
