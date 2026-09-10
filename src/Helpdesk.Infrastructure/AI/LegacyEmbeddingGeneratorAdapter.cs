using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Infrastructure.AI;

public sealed class LegacyEmbeddingGeneratorAdapter(
    AiProvider provider,
    string modelId,
    int? dimensions,
    ISecretProtector protector,
    IHttpClientFactory httpClientFactory,
    ILogger<LegacyEmbeddingGeneratorAdapter> logger) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly AiProvider _provider = provider;
    private readonly string _modelId = modelId;
    private readonly int? _dimensions = dimensions;
    private readonly ISecretProtector _protector = protector;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LegacyEmbeddingGeneratorAdapter> _logger = logger;

    public void Dispose()
    {
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputArray = values as string[] ?? values.ToArray();
        var vectors = await CreateEmbeddingsAsync(inputArray, cancellationToken);
        var embeddings = vectors
            .Select(vector => new Embedding<float>(vector))
            .ToArray();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    private async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        string[] inputArray,
        CancellationToken token)
    {
        var baseUrl = _provider.BaseUrl?.TrimEnd('/')
            ?? throw new InvalidOperationException("Provider.BaseUrl is empty.");

        var apiKey = _protector.Unprotect(_provider.ApiKeyEncrypted);

        var client = _httpClientFactory.CreateClient("OpenAICompatible");
        client.Timeout = TimeSpan.FromSeconds(2);

        static string TrimSuffix(string value, string suffix) =>
            value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? value[..^suffix.Length] : value;

        var candidates = new List<(Uri uri, string kind)>
        {
            (new Uri((baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? baseUrl : baseUrl + "/v1") + "/embeddings"), "openai")
        };

        var normalizedRoot = TrimSuffix(TrimSuffix(baseUrl, "/v1"), "/");
        normalizedRoot = TrimSuffix(normalizedRoot, "/ollama");

        candidates.Add((new Uri(normalizedRoot + "/api/embeddings"), "generic"));
        candidates.Add((new Uri(normalizedRoot + "/api/embed"), "embed-legacy"));
        candidates.Add((new Uri(normalizedRoot + "/ollama/api/embeddings"), "generic"));
        candidates.Add((new Uri(normalizedRoot + "/ollama/api/embed"), "embed-legacy"));

        string lastErrBody = string.Empty;
        HttpStatusCode? lastStatus = null;

        foreach (var (uri, kind) in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
                request.Content = JsonContent.Create(new
                {
                    model = _modelId,
                    input = inputArray,
                    dimensions = _dimensions
                });

                _logger.LogInformation(
                    "Embeddings attempt: {Kind} -> {Url} (model={Model}, count={Count})",
                    kind,
                    uri,
                    _modelId,
                    inputArray.Length);

                using var response = await client.SendAsync(request, token);
                if (!response.IsSuccessStatusCode)
                {
                    lastStatus = response.StatusCode;
                    lastErrBody = await response.Content.ReadAsStringAsync(token);

                    if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
                    {
                        _logger.LogWarning(
                            "Embeddings attempt failed {Status} at {Url}. Trying next candidate.",
                            (int)response.StatusCode,
                            uri);
                        continue;
                    }

                    throw new EmbeddingUnavailableException($"Embeddings {response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                return ParseEmbeddingVectors(doc.RootElement);
            }
            catch (HttpRequestException ex) when (lastStatus is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            {
                _logger.LogWarning(ex, "Embeddings candidate failed at {Url}; falling back.", uri);
            }
        }

        throw new InvalidOperationException(
            $"Could not reach a working embeddings endpoint for base '{baseUrl}'. Last status={(int?)lastStatus} body='{lastErrBody}'.");
    }

    private static IReadOnlyList<float[]> ParseEmbeddingVectors(JsonElement rootEl)
    {
        var result = new List<float[]>();

        if (rootEl.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("embedding", out var embedding))
                {
                    result.Add(embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray());
                }
            }

            if (result.Count > 0)
            {
                return result;
            }
        }

        if (rootEl.TryGetProperty("embeddings", out var embeddingsArray) && embeddingsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in embeddingsArray.EnumerateArray())
            {
                var vectorEl = item;
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("embedding", out var inner))
                {
                    vectorEl = inner;
                }

                if (vectorEl.ValueKind == JsonValueKind.Array)
                {
                    result.Add(vectorEl.EnumerateArray().Select(value => value.GetSingle()).ToArray());
                }
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Embeddings response did not contain recognized vector data.");
        }

        return result;
    }
}
