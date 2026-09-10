using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.KB;

public sealed record KnowledgeEvidenceChunk(
    Guid KnowledgeBaseArticleId,
    string ChunkId,
    string SourceId,
    string DocumentTitle,
    int ChunkIndex,
    string Text,
    IReadOnlyDictionary<string, string> Metadata,
    double Score);

public sealed record KnowledgeRetrievalResult(
    KnowledgeBaseArticle Article,
    double Score,
    IReadOnlyList<KnowledgeEvidenceChunk> Evidence,
    bool IsHighConfidence,
    string ConfidenceLabel);
