using Helpdesk.Shared.Models;

namespace Helpdesk.Application.RequestTasks;

public interface IRequestTaskGenerationService
{
    Task<int> GenerateForRequestAsync(Request request, CancellationToken cancellationToken);
}
