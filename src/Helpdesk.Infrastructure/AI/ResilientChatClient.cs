using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.AI;

internal sealed class ResilientChatClient(
    IAiClient aiClient,
    IReadOnlyList<AiProvider> providers,
    IReadOnlyDictionary<Guid, string> modelMap,
    int maxAttempts,
    IAiOperationAuditService auditService,
    string? organizationId,
    string? subjectId,
    string? correlationId,
    string? scenario,
    ILogger<ResilientChatClient> logger) : IChatClient
{
    private readonly IAiClient _aiClient = aiClient;
    private readonly IReadOnlyList<AiProvider> _providers = providers;
    private readonly IReadOnlyDictionary<Guid, string> _modelMap = modelMap;
    private readonly int _maxAttempts = Math.Max(1, maxAttempts);
    private readonly IAiOperationAuditService _auditService = auditService;
    private readonly string? _organizationId = organizationId;
    private readonly string? _subjectId = subjectId;
    private readonly string? _correlationId = correlationId;
    private readonly string? _scenario = scenario;
    private readonly ILogger<ResilientChatClient> _logger = logger;

    public void Dispose()
    {
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = string.Join(
            Environment.NewLine + Environment.NewLine,
            messages.Select(message => $"{message.Role}: {FlattenContents(message)}"));

        Exception? lastError = null;
        var failures = new List<string>();
        for (var attempt = 0; attempt < _maxAttempts; attempt++)
        {
            var provider = _providers[attempt % _providers.Count];
            var selectedModel = options?.ModelId ?? _modelMap[provider.Id];

            try
            {
                var content = await _aiClient.ChatAsync(provider, prompt, selectedModel, cancellationToken);
                if (attempt > 0)
                {
                    _logger.LogWarning(
                        "AI chat fallback succeeded on provider {ProviderName} after {AttemptCount} attempts.",
                        provider.Name,
                        attempt + 1);

                    await _auditService.RecordAsync(
                        new AiOperationAuditEntry(
                            "runtime-chat-fallback",
                            _organizationId,
                            provider.Name,
                            selectedModel,
                            _correlationId,
                            _subjectId,
                            BuildFallbackNotes(attempt + 1, failures, _scenario)),
                        cancellationToken);
                }

                return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(
                    ex,
                    "AI chat attempt {Attempt} failed for provider {ProviderName}.",
                    attempt + 1,
                    provider.Name);
                failures.Add($"{provider.Name}/{selectedModel}: {ex.GetType().Name}");
            }
        }

        await _auditService.RecordAsync(
            new AiOperationAuditEntry(
                "runtime-chat-failure",
                _organizationId,
                string.Join(" -> ", _providers.Select(x => x.Name)),
                options?.ModelId ?? _modelMap[_providers[0].Id],
                _correlationId,
                _subjectId,
                BuildFailureNotes(failures, _scenario)),
            cancellationToken);

        throw new InvalidOperationException("All configured AI chat providers failed.", lastError);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetEmptyStream();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    private static string FlattenContents(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text;
        }

        return string.Join(" ", message.Contents.Select(content => content.ToString()));
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> GetEmptyStream()
    {
        yield break;
    }

    private static string BuildFallbackNotes(int attempts, IReadOnlyCollection<string> failures, string? scenario)
    {
        var parts = new List<string> { $"Fallback succeeded after {attempts} attempts." };
        if (!string.IsNullOrWhiteSpace(scenario))
        {
            parts.Add($"Scenario: {scenario}.");
        }

        if (failures.Count > 0)
        {
            parts.Add($"Prior failures: {string.Join("; ", failures)}.");
        }

        return string.Join(" ", parts);
    }

    private static string BuildFailureNotes(IReadOnlyCollection<string> failures, string? scenario)
    {
        var parts = new List<string> { "All configured chat providers failed." };
        if (!string.IsNullOrWhiteSpace(scenario))
        {
            parts.Add($"Scenario: {scenario}.");
        }

        if (failures.Count > 0)
        {
            parts.Add($"Failures: {string.Join("; ", failures)}.");
        }

        return string.Join(" ", parts);
    }
}
