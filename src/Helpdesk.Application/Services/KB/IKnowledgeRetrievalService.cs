namespace Helpdesk.Application.Services.KB;

public interface IKnowledgeRetrievalService
{
    Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        string organizationId,
        string query,
        int limit,
        CancellationToken token);
}
