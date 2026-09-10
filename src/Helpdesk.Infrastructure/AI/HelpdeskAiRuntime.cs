using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.AI;

public sealed class HelpdeskAiRuntime(
    HelpdeskDbContext db,
    IAiClient aiClient,
    IAiOperationAuditService aiAudit,
    ISecretProtector protector,
    IHttpClientFactory httpClientFactory,
    ILogger<LegacyEmbeddingGeneratorAdapter> embeddingLogger,
    ILoggerFactory loggerFactory) : IAiRuntime
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IAiClient _aiClient = aiClient;
    private readonly IAiOperationAuditService _aiAudit = aiAudit;
    private readonly ISecretProtector _protector = protector;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LegacyEmbeddingGeneratorAdapter> _embeddingLogger = embeddingLogger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public async Task<AiResolvedChatClient> CreateChatClientAsync(AiChatRuntimeRequest request, CancellationToken token)
    {
        var (providers, modelMap, maxAttempts) = await ResolveChatProvidersAsync(request, token);
        var primaryModelId = modelMap[providers[0].Id];

        return new AiResolvedChatClient(
            new ResilientChatClient(
                _aiClient,
                providers,
                modelMap,
                maxAttempts,
                _aiAudit,
                request.OrganizationId,
                request.SubjectId,
                request.CorrelationId,
                request.Scenario,
                _loggerFactory.CreateLogger<ResilientChatClient>()),
            string.Join(" -> ", providers.Select(x => x.Name)),
            primaryModelId);
    }

    public async Task<AiResolvedEmbeddingGenerator> CreateEmbeddingGeneratorAsync(AiEmbeddingRuntimeRequest request, CancellationToken token)
    {
        var settings = await _db.OrganizationAiKbSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, token)
            ?? throw new InvalidOperationException($"No AI/KB settings found for org {request.OrganizationId}");

        var providers = await ResolveEmbeddingProvidersAsync(request, settings, token);
        var dimensions = request.Dimensions ?? settings.EmbeddingDimensions;
        var generator = new ResilientEmbeddingGenerator(
            providers,
            dimensions,
            _protector,
            _httpClientFactory,
            _loggerFactory);

        return new AiResolvedEmbeddingGenerator(
            generator,
            string.Join(" -> ", providers.Select(x => x.Provider.Name)),
            providers[0].ModelId,
            dimensions);
    }

    private async Task<(List<AiProvider> Providers, Dictionary<Guid, string> ModelMap, int MaxAttempts)> ResolveChatProvidersAsync(
        AiChatRuntimeRequest request,
        CancellationToken token)
    {
        if (request.ProviderId.HasValue)
        {
            var explicitProvider = await _db.AiProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProviderId.Value && p.IsEnabled, token)
                ?? throw new InvalidOperationException($"AI provider not found or disabled: {request.ProviderId}");
            var explicitModel = request.ModelId ?? explicitProvider.DefaultModel
                ?? throw new InvalidOperationException($"Provider '{explicitProvider.Name}' has no default model configured.");
            return ([explicitProvider], new Dictionary<Guid, string> { [explicitProvider.Id] = explicitModel }, 1);
        }

        OrganizationAiKbSettings? settings = null;
        var providers = new List<AiProvider>();
        var modelMap = new Dictionary<Guid, string>();

        if (!string.IsNullOrWhiteSpace(request.OrganizationId))
        {
            settings = await _db.OrganizationAiKbSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, token);

            if (settings is not null
                && Guid.TryParse(settings.KnowledgeProviderId, out var knowledgeProviderId))
            {
                var knowledgeProvider = await _db.AiProviders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == knowledgeProviderId && p.IsEnabled, token);
                if (knowledgeProvider is not null)
                {
                    providers.Add(knowledgeProvider);
                    modelMap[knowledgeProvider.Id] = request.ModelId
                        ?? settings.KnowledgeModelName
                        ?? knowledgeProvider.DefaultModel
                        ?? throw new InvalidOperationException($"Provider '{knowledgeProvider.Name}' has no default model configured.");
                }
            }
        }

        if (providers.Count == 0)
        {
            var fallbackPrimary = await _db.AiProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsEnabled, token)
                ?? throw new InvalidOperationException("No enabled AI provider configured.");
            providers.Add(fallbackPrimary);
            modelMap[fallbackPrimary.Id] = request.ModelId
                ?? fallbackPrimary.DefaultModel
                ?? throw new InvalidOperationException($"Provider '{fallbackPrimary.Name}' has no default model configured.");
        }

        if (settings?.EnableProviderFallback == true)
        {
            var providerIds = providers.Select(x => x.Id).ToList();
            var fallbackProviders = await _db.AiProviders
                .AsNoTracking()
                .Where(p => p.IsEnabled && !providerIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync(token);

            foreach (var provider in fallbackProviders)
            {
                var modelId = request.ModelId
                    ?? provider.DefaultModel
                    ?? settings.KnowledgeModelName
                    ?? providers[0].DefaultModel;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    continue;
                }

                providers.Add(provider);
                modelMap[provider.Id] = modelId;
            }
        }

        return (providers, modelMap, settings?.MaxProviderAttempts is > 0 ? settings.MaxProviderAttempts : 1);
    }

    private async Task<List<(AiProvider Provider, string ModelId)>> ResolveEmbeddingProvidersAsync(
        AiEmbeddingRuntimeRequest request,
        OrganizationAiKbSettings settings,
        CancellationToken token)
    {
        var results = new List<(AiProvider Provider, string ModelId)>();
        var providerIds = new List<Guid>();

        if (request.ProviderId.HasValue)
        {
            providerIds.Add(request.ProviderId.Value);
        }
        else if (Guid.TryParse(settings.EmbeddingProviderId, out var configuredProviderId))
        {
            providerIds.Add(configuredProviderId);
        }

        foreach (var providerId in providerIds)
        {
            var provider = await _db.AiProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == providerId && p.IsEnabled, token);
            var modelId = request.ModelId ?? settings.EmbeddingModel ?? provider?.DefaultModel;
            if (provider is not null && !string.IsNullOrWhiteSpace(modelId))
            {
                results.Add((provider, modelId));
            }
        }

        if (settings.EnableProviderFallback)
        {
            var fallbackProviders = await _db.AiProviders
                .AsNoTracking()
                .Where(p => p.IsEnabled && !providerIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync(token);

            foreach (var provider in fallbackProviders)
            {
                var modelId = request.ModelId ?? settings.EmbeddingModel ?? provider.DefaultModel;
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    results.Add((provider, modelId));
                }
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidOperationException($"No embedding provider is configured for org {request.OrganizationId}.");
        }

        return results;
    }
}
