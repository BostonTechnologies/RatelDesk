namespace Helpdesk.Application.Services.KB;

public sealed record KnowledgeVectorRecord(
    Guid KnowledgeBaseArticleId,
    string OrganizationId,
    string SourceType,
    string SourceId,
    string DocumentTitle,
    int ChunkIndex,
    string ChunkId,
    string Text,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<float> Vector,
    DateTime CreatedAt);

public sealed record KnowledgeVectorFilter(
    IReadOnlyList<Guid>? ArticleIds = null,
    IReadOnlyList<string>? SourceTypes = null,
    IReadOnlyList<string>? SourceIds = null);

public sealed record KnowledgeVectorMatch(
    Guid KnowledgeBaseArticleId,
    string OrganizationId,
    string SourceId,
    string DocumentTitle,
    int ChunkIndex,
    string ChunkId,
    string Text,
    IReadOnlyDictionary<string, string> Metadata,
    double Score);
