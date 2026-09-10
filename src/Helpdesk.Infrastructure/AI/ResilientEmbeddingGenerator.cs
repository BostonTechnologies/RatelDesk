using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.AI;

internal sealed class ResilientEmbeddingGenerator(
    IReadOnlyList<(AiProvider Provider, string ModelId)> providers,
    int? dimensions,
    ISecretProtector protector,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IReadOnlyList<(AiProvider Provider, string ModelId)> _providers = providers;
    private readonly int? _dimensions = dimensions;
    private readonly ISecretProtector _protector = protector;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public void Dispose()
    {
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;
        foreach (var (provider, modelId) in _providers)
        {
            try
            {
                var generator = new LegacyEmbeddingGeneratorAdapter(
                    provider,
                    modelId,
                    _dimensions,
                    _protector,
                    _httpClientFactory,
                    _loggerFactory.CreateLogger<LegacyEmbeddingGeneratorAdapter>());
                return await generator.GenerateAsync(values, options, cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _loggerFactory.CreateLogger<ResilientEmbeddingGenerator>()
                    .LogWarning(ex, "Embedding generation failed for provider {ProviderName}.", provider.Name);
            }
        }

        throw new InvalidOperationException("All configured embedding providers failed.", lastError);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }
}
