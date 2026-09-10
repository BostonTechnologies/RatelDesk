namespace Helpdesk.Application.Services.KB;

public interface IKnowledgeVectorStore
{
    Task ReplaceArticleChunksAsync(
        Guid articleId,
        string organizationId,
        string sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeVectorRecord> chunks,
        CancellationToken token);

    Task<IReadOnlyList<KnowledgeVectorMatch>> SearchAsync(
        string organizationId,
        IReadOnlyList<float> vector,
        int limit,
        KnowledgeVectorFilter? filter,
        CancellationToken token);
}
