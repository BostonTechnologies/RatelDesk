namespace Helpdesk.Shared.DTOs.Article;

public record KnowledgeSuggestionEvidence(string ChunkId, string SourceId, string Excerpt, double Score);

public record KnowledgeSuggestion(
    string ArticleId,
    string Title,
    string Excerpt,
    string Link,
    double Score,
    string ConfidenceLabel = "Low",
    bool IsHighConfidence = false,
    bool RequiresClarification = false,
    string? ClarificationPrompt = null,
    IReadOnlyList<KnowledgeSuggestionEvidence>? Evidence = null,
    KnowledgeArticleAutomationTargetDto? AutomationTarget = null);
