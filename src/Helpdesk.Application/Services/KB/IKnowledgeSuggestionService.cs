
using System.Collections.Generic;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.KB;

public interface IKnowledgeSuggestionService
{
    Task<IEnumerable<TicketKnowledgeSuggestion>> SuggestAsync(Ticket ticket, CancellationToken token);
}
