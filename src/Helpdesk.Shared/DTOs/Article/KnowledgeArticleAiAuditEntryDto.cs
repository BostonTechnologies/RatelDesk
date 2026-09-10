namespace Helpdesk.Shared.DTOs.Article;

public sealed class KnowledgeArticleAiAuditEntryDto
{
    public Guid Id { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? Notes { get; set; }
    public string? SourceSubjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
