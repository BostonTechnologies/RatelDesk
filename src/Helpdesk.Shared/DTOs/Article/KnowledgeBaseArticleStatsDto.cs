namespace Helpdesk.Shared.DTOs.Article;

public class KnowledgeBaseArticleStatsDto
{
    public int ChunkCount { get; set; }
    public string SourceType { get; set; } = "KB";
    public string SourceId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public DateTime? LastIndexedAt { get; set; }
}
