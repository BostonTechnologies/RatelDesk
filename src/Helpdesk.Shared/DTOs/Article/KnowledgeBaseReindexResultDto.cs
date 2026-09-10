namespace Helpdesk.Shared.DTOs.Article;

public class KnowledgeBaseReindexResultDto
{
    public int RequestedArticles { get; set; }
    public int ReindexedArticles { get; set; }
    public int SkippedArticles { get; set; }
}
