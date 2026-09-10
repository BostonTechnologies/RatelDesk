using Dodo.Primitives;

namespace Helpdesk.Shared.Models;

public class KnowledgeBaseArticle
{
    public Guid Id { get; set; } = Guid.Parse(Uuid.CreateVersion7().ToString());
    public string OrganizationId { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Problem { get; set; }
    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public string? Application { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? SourceIncidentId { get; set; }
    public string? AutomationBindingId { get; set; }
    public string? AutomationRequestFormId { get; set; }
    public Guid? AutomationTaskTemplateId { get; set; }
    public string? AutomationTaskTemplateName { get; set; }
    public string? AutomationOrchestrationRequestDefinitionId { get; set; }
    public string? AutomationOrchestrationJobDefinitionId { get; set; }
    public KnowledgeBaseArticleState State { get; set; } = KnowledgeBaseArticleState.Draft;
    public string? LinkedTicketId { get; set; }
    public string? CreatedByModel { get; set; }
    public DateTime? LastRegeneratedAt { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<KnowledgeEmbedding> Embeddings { get; set; } = new List<KnowledgeEmbedding>();
}
