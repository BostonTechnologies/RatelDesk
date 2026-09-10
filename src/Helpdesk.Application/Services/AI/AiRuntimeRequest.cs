using Microsoft.Extensions.AI;

namespace Helpdesk.Application.Services.AI;

public sealed record AiChatRuntimeRequest(
    string? OrganizationId,
    Guid? ProviderId = null,
    string? ModelId = null,
    string? Scenario = null,
    string? SubjectId = null,
    string? CorrelationId = null);

public sealed record AiEmbeddingRuntimeRequest(
    string OrganizationId,
    Guid? ProviderId = null,
    string? ModelId = null,
    int? Dimensions = null,
    string? Scenario = null);

public sealed record AiResolvedChatClient(
    IChatClient Client,
    string ProviderName,
    string ModelId);

public sealed record AiResolvedEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> Generator,
    string ProviderName,
    string ModelId,
    int? Dimensions);
