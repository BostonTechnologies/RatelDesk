namespace Helpdesk.Shared.DTOs.Article;

public class KnowledgeBaseHealthDto
{
    public int TotalArticles { get; set; }
    public int DraftArticles { get; set; }
    public int PublishedArticles { get; set; }
    public int UnresolvedTicketsWithoutKb { get; set; }
    public int IndexedArticles { get; set; }
    public int IndexedChunkCount { get; set; }
    public int OrphanedChunkCount { get; set; }
    public int EmailChunkCount { get; set; }
    public double IndexedArticleCoverage { get; set; }
    public int SuggestionFeedbackCount { get; set; }
    public int HelpfulSuggestionCount { get; set; }
    public int NotHelpfulSuggestionCount { get; set; }
    public double SuggestionHelpfulRate { get; set; }
    public int AutomationFeedbackCount { get; set; }
    public int AutomationResolvedCount { get; set; }
    public int AutomationNotResolvedCount { get; set; }
    public double AutomationResolvedRate { get; set; }
    public int AiAuditCount { get; set; }
    public DateTimeOffset? LastAiAuditAt { get; set; }
    public int KnowledgeBuildAuditCount { get; set; }
    public int KnowledgeDraftFromTicketAuditCount { get; set; }
    public int RequesterReplyAuditCount { get; set; }
    public int RequesterReplyApprovedAuditCount { get; set; }
    public int AutomationApprovedAuditCount { get; set; }
    public int RuntimeFallbackAuditCount { get; set; }
    public int RuntimeFailureAuditCount { get; set; }
    public DateTimeOffset? LastRuntimeAuditAt { get; set; }
    public string? TopRuntimeFallbackProvider { get; set; }
    public int TopRuntimeFallbackCount { get; set; }
    public string? TopRuntimeFailureProvider { get; set; }
    public int TopRuntimeFailureCount { get; set; }
    public List<string> RecentRuntimeEvidence { get; set; } = new();
}
