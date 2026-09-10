using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Application.Services.KB;

public sealed class KnowledgeRetrievalService(
    HelpdeskDbContext db,
    IEmbeddingService embeddingService,
    IKnowledgeVectorStore vectorStore) : IKnowledgeRetrievalService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IKnowledgeVectorStore _vectorStore = vectorStore;

    public async Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        string organizationId,
        string query,
        int limit,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        var vector = await _embeddingService.CreateEmbeddingAsync(organizationId, query, token);
        var matches = await _vectorStore.SearchAsync(
            organizationId,
            vector,
            Math.Max(limit * 3, limit),
            new KnowledgeVectorFilter(SourceTypes: ["KB"]),
            token);
        if (matches.Count == 0)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        var articleIds = matches
            .Select(match => match.KnowledgeBaseArticleId)
            .Distinct()
            .Take(limit)
            .ToList();

        var articles = await _db.KnowledgeBaseArticles
            .AsNoTracking()
            .Where(article => articleIds.Contains(article.Id))
            .ToDictionaryAsync(article => article.Id, token);

        var grouped = matches
            .Where(match => articles.ContainsKey(match.KnowledgeBaseArticleId))
            .GroupBy(match => match.KnowledgeBaseArticleId)
            .OrderByDescending(group => group.Max(match => match.Score))
            .Take(limit);

        return grouped
            .Select(group =>
            {
                var evidence = group
                    .OrderByDescending(match => match.Score)
                    .Take(3)
                    .Select(match => new KnowledgeEvidenceChunk(
                        match.KnowledgeBaseArticleId,
                        match.ChunkId,
                        match.SourceId,
                        match.DocumentTitle,
                        match.ChunkIndex,
                        match.Text,
                        match.Metadata,
                        match.Score))
                    .ToList();

                var topScore = evidence.Count == 0 ? 0 : evidence[0].Score;
                return new KnowledgeRetrievalResult(
                    articles[group.Key],
                    topScore,
                    evidence,
                    topScore >= 0.75d,
                    topScore >= 0.85d ? "High" : topScore >= 0.6d ? "Medium" : "Low");
            })
            .ToList();
    }
}
