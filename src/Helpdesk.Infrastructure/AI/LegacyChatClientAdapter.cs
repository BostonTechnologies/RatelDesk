using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.AI;

namespace Helpdesk.Infrastructure.AI;

internal sealed class LegacyChatClientAdapter(
    IAiClient aiClient,
    AiProvider provider,
    string modelId) : IChatClient
{
    private readonly IAiClient _aiClient = aiClient;
    private readonly AiProvider _provider = provider;
    private readonly string _modelId = modelId;

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

        var selectedModel = options?.ModelId ?? _modelId;
        var content = await _aiClient.ChatAsync(_provider, prompt, selectedModel, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
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
}
