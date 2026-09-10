using System;
using System.Collections.Generic;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.KB;

public class NoOpKnowledgeSuggestionService : IKnowledgeSuggestionService
{
    public Task<IEnumerable<TicketKnowledgeSuggestion>> SuggestAsync(Ticket ticket, CancellationToken token) =>
        Task.FromResult<IEnumerable<TicketKnowledgeSuggestion>>(Array.Empty<TicketKnowledgeSuggestion>());
}
