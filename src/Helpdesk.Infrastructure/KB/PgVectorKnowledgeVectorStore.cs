using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using System.Text.Json;

namespace Helpdesk.Infrastructure.KB;

public sealed class PgVectorKnowledgeVectorStore(
    HelpdeskDbContext db) : IKnowledgeVectorStore
{
    private readonly HelpdeskDbContext _db = db;

    private sealed class SearchRow
    {
        public Guid KnowledgeBaseArticleId { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string ChunkId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public double Score { get; set; }
    }

    public async Task ReplaceArticleChunksAsync(
        Guid articleId,
        string organizationId,
        string sourceType,
        string sourceId,
        IReadOnlyList<KnowledgeVectorRecord> chunks,
        CancellationToken token)
    {
        var existing = await _db.KnowledgeEmbeddings
            .Where(x => x.KnowledgeBaseArticleId == articleId)
            .ToListAsync(token);

        if (existing.Count > 0)
        {
            _db.KnowledgeEmbeddings.RemoveRange(existing);
        }

        foreach (var chunk in chunks)
        {
            _db.KnowledgeEmbeddings.Add(new KnowledgeEmbedding
            {
                Id = Guid.NewGuid(),
                KnowledgeBaseArticleId = articleId,
                OrganizationId = organizationId,
                SourceType = sourceType,
                SourceId = sourceId,
                DocumentTitle = chunk.DocumentTitle,
                ChunkIndex = chunk.ChunkIndex,
                ChunkId = chunk.ChunkId,
                Text = chunk.Text,
                MetadataJson = SerializeMetadata(chunk.Metadata),
                Vector = new Vector(chunk.Vector.ToArray()),
                CreatedAt = chunk.CreatedAt
            });
        }
    }

    public async Task<IReadOnlyList<KnowledgeVectorMatch>> SearchAsync(
        string organizationId,
        IReadOnlyList<float> vector,
        int limit,
        KnowledgeVectorFilter? filter,
        CancellationToken token)
    {
        if (!_db.Database.IsRelational())
        {
            var query = _db.KnowledgeEmbeddings
                .Where(x => x.OrganizationId == organizationId && x.KnowledgeBaseArticleId.HasValue);

            query = ApplyFilter(query, filter);

            return query
                .AsEnumerable()
                .Select(x => new KnowledgeVectorMatch(
                    x.KnowledgeBaseArticleId!.Value,
                    x.OrganizationId,
                    x.SourceId,
                    x.DocumentTitle,
                    x.ChunkIndex,
                    x.ChunkId,
                    x.Text,
                    DeserializeMetadata(x.MetadataJson),
                    1 - Distance(vector, x.Vector.ToArray())))
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
        }

        var sql = @"
        SELECT ""KnowledgeBaseArticleId"",
               ""OrganizationId"",
               ""SourceId"",
               ""DocumentTitle"",
               ""ChunkIndex"",
               ""ChunkId"",
               ""Text"",
               ""MetadataJson"",
               COALESCE(1 - (""Vector"" <#> @vec), 0) AS ""Score""
        FROM ""KnowledgeEmbeddings""
        WHERE ""OrganizationId"" = @org";

        var parameters = new List<object>
        {
            new NpgsqlParameter<Vector>("vec", new Vector(vector.ToArray())),
            new NpgsqlParameter<string>("org", organizationId),
        };

        if (filter?.SourceTypes?.Count > 0)
        {
            sql += " AND \"SourceType\" = ANY (@sourceTypes)";
            parameters.Add(new NpgsqlParameter<string[]>("sourceTypes", filter.SourceTypes.ToArray()));
        }

        if (filter?.SourceIds?.Count > 0)
        {
            sql += " AND \"SourceId\" = ANY (@sourceIds)";
            parameters.Add(new NpgsqlParameter<string[]>("sourceIds", filter.SourceIds.ToArray()));
        }

        if (filter?.ArticleIds?.Count > 0)
        {
            sql += " AND \"KnowledgeBaseArticleId\" = ANY (@articleIds)";
            parameters.Add(new NpgsqlParameter<Guid[]>("articleIds", filter.ArticleIds.ToArray()));
        }

        sql += @"
        ORDER BY ""Vector"" <#> @vec
        LIMIT @lim";

        parameters.Add(new NpgsqlParameter<int>("lim", limit));

        var rows = await _db.Database
            .SqlQueryRaw<SearchRow>(sql, parameters.ToArray())
            .ToListAsync(token);

        return rows
            .Select(row => new KnowledgeVectorMatch(
                row.KnowledgeBaseArticleId,
                row.OrganizationId,
                row.SourceId,
                row.DocumentTitle,
                row.ChunkIndex,
                row.ChunkId,
                row.Text,
                DeserializeMetadata(row.MetadataJson),
                row.Score))
            .ToList();
    }

    private static IQueryable<KnowledgeEmbedding> ApplyFilter(
        IQueryable<KnowledgeEmbedding> query,
        KnowledgeVectorFilter? filter)
    {
        if (filter?.SourceTypes?.Count > 0)
        {
            query = query.Where(x => filter.SourceTypes.Contains(x.SourceType));
        }

        if (filter?.SourceIds?.Count > 0)
        {
            query = query.Where(x => filter.SourceIds.Contains(x.SourceId));
        }

        if (filter?.ArticleIds?.Count > 0)
        {
            query = query.Where(x => x.KnowledgeBaseArticleId.HasValue && filter.ArticleIds.Contains(x.KnowledgeBaseArticleId.Value));
        }

        return query;
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata);

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(string? metadataJson) =>
        string.IsNullOrWhiteSpace(metadataJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson) ?? new Dictionary<string, string>();

    private static double Distance(IReadOnlyList<float> left, float[] right)
    {
        double sum = 0;
        var length = Math.Min(left.Count, right.Length);
        for (var i = 0; i < length; i++)
        {
            var diff = left[i] - right[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }
}
