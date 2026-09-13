using Helpdesk.Application.Services.KB;

namespace Helpdesk.Infrastructure.KB;

public sealed class NoOpKnowledgeVectorStore : IKnowledgeVectorStore
{
    public Task ReplaceArticleChunksAsync(
        Guid articleId,
        string organizationId,
        string sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeVectorRecord> chunks,
        CancellationToken token) => Task.CompletedTask;

    public Task<IReadOnlyList<KnowledgeVectorMatch>> SearchAsync(
        string organizationId,
        IReadOnlyList<float> vector,
        int limit,
        KnowledgeVectorFilter? filter,
        CancellationToken token) => Task.FromResult<IReadOnlyList<KnowledgeVectorMatch>>([]);
}
