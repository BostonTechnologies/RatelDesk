using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class OrganizationAiKbSettings
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string OrganizationId { get; set; } = string.Empty;

    // Feature flags
    public bool EnableAiSearch { get; set; }
    public bool EnableAiAnswers { get; set; }

    // Ranking/thresholds
    public double SearchThreshold { get; set; }
    public double AnswerThreshold { get; set; }

    // Embeddings
    public string? EmbeddingProviderId { get; set; }
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }

    // Knowledge model
    public string? KnowledgeProviderId { get; set; }
    public string? KnowledgeModelName { get; set; }

    // Other
    public int SuggestionLimit { get; set; } = 3;
    public string? AllowedServicesCsv { get; set; }
    public bool EnableProviderFallback { get; set; } = true;
    public int MaxProviderAttempts { get; set; } = 2;
    public int MinimumSuggestionFeedbackCount { get; set; } = 5;
    public double MinimumSuggestionHelpfulRate { get; set; } = 0.6;
    public int MinimumAutomationFeedbackCount { get; set; } = 3;
    public double MinimumAutomationResolvedRate { get; set; } = 0.5;
}
