using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface IRequesterReplyDraftService
{
    Task<RequesterReplyDraftDto?> GenerateAsync(Ticket ticket, CancellationToken token);
}
