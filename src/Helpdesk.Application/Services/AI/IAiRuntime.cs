namespace Helpdesk.Application.Services.AI;

public interface IAiRuntime
{
    Task<AiResolvedChatClient> CreateChatClientAsync(AiChatRuntimeRequest request, CancellationToken token);
    Task<AiResolvedEmbeddingGenerator> CreateEmbeddingGeneratorAsync(AiEmbeddingRuntimeRequest request, CancellationToken token);
}
