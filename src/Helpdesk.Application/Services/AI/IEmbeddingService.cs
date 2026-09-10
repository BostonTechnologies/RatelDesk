using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.AI;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string orgId, string text, CancellationToken token);
    Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(string orgId, IEnumerable<string> texts, CancellationToken token);
}
