using System.Text.Json.Serialization;

namespace HelpDesk.NewWeb.Models;

public sealed class AiKbSettingsModel
{
    // API fields
    [JsonPropertyName("enableAiSearch")] public bool EnableAi { get; set; }        // UI: "Enable AI"
    [JsonPropertyName("enableAiAnswers")] public bool EnableKb { get; set; }        // UI: "Enable KB"

    [JsonPropertyName("providerId")] public string? ProviderId { get; set; }
    [JsonPropertyName("modelName")] public string? ModelName { get; set; }

    [JsonPropertyName("suggestionLimit")] public int SuggestionLimit { get; set; } = 3;

    [JsonPropertyName("embeddingModel")] public string? EmbeddingModel { get; set; }
    [JsonPropertyName("embeddingDimensions")] public int EmbeddingDimensions { get; set; }

    [JsonPropertyName("allowedServicesCsv")] public string? AllowedServicesCsv { get; set; }
}
