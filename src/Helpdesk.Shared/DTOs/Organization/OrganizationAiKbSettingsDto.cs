namespace Helpdesk.Shared.DTOs.Organization;

public class OrganizationAiKbSettingsDto
{
    public bool EnableAiSearch { get; set; }
    public bool EnableAiAnswers { get; set; }
    public double SearchThreshold { get; set; }
    public double AnswerThreshold { get; set; }

    public string? EmbeddingProviderId { get; set; }
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }

    public string? KnowledgeProviderId { get; set; }
    public string? KnowledgeModelName { get; set; }

    public int SuggestionLimit { get; set; }
    public string? AllowedServicesCsv { get; set; }
    public bool EnableProviderFallback { get; set; }
    public int MaxProviderAttempts { get; set; }
    public int MinimumSuggestionFeedbackCount { get; set; }
    public double MinimumSuggestionHelpfulRate { get; set; }
    public int MinimumAutomationFeedbackCount { get; set; }
    public double MinimumAutomationResolvedRate { get; set; }
}
