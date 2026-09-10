namespace Helpdesk.Shared.DTOs.Organization;

public sealed class OrganizationAiReadinessDto
{
    public int SuggestionFeedbackCount { get; set; }
    public int HelpfulSuggestionCount { get; set; }
    public double SuggestionHelpfulRate { get; set; }
    public int AutomationFeedbackCount { get; set; }
    public int AutomationResolvedCount { get; set; }
    public double AutomationResolvedRate { get; set; }
    public bool SuggestionGateMet { get; set; }
    public bool AutomationGateMet { get; set; }
    public bool ProductionReady { get; set; }
    public List<string> BlockingReasons { get; set; } = new();
    public int AiAuditCount { get; set; }
    public DateTimeOffset? LastAiAuditAt { get; set; }
}
