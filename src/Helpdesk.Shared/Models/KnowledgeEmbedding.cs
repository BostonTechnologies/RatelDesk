using Dodo.Primitives;
using Pgvector;

namespace Helpdesk.Shared.Models;

public class KnowledgeEmbedding
{
    public Guid Id { get; set; } = Guid.Parse(Uuid.CreateVersion7().ToString());
    public string OrganizationId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "KB";
    public string SourceId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public double LastScore { get; set; }
    public Guid? KnowledgeBaseArticleId { get; set; }
    public KnowledgeBaseArticle Article { get; set; } = null!;
    public Vector Vector { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
