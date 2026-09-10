namespace Helpdesk.Shared.DTOs.Article;

public sealed class KnowledgeArticleAutomationTargetDto
{
    public string? BindingId { get; set; }
    public string? RequestFormId { get; set; }
    public Guid? TaskTemplateId { get; set; }
    public string? TaskTemplateName { get; set; }
    public string? OrchestrationRequestDefinitionId { get; set; }
    public string? OrchestrationJobDefinitionId { get; set; }
    public bool Enabled { get; set; }
}
