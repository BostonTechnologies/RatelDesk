using Helpdesk.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Application.Services.AI;

public class EmbeddingService(
    HelpdeskDbContext db,
    IAiRuntime aiRuntime,
    ILogger<EmbeddingService> logger) : IEmbeddingService
{
    private readonly HelpdeskDbContext _db = db;
    private readonly IAiRuntime _aiRuntime = aiRuntime;
    private readonly ILogger<EmbeddingService> _logger = logger;

    public async Task<float[]> CreateEmbeddingAsync(string orgId, string text, CancellationToken token)
    {
        var embeddings = await CreateEmbeddingsAsync(orgId, new[] { text }, token);
        return embeddings.First();
    }

    public async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        string orgId,
        IEnumerable<string> texts,
        CancellationToken token)
    {
        var inputArray = texts as string[] ?? texts.ToArray();
        var runtime = await _aiRuntime.CreateEmbeddingGeneratorAsync(
            new AiEmbeddingRuntimeRequest(orgId, Scenario: "kb-embedding"),
            token);
        var generated = await runtime.Generator.GenerateAsync(inputArray, cancellationToken: token);
        _logger.LogInformation(
            "Embeddings generated via AI runtime for org {OrgId} provider={Provider} model={Model} count={Count}",
            orgId,
            runtime.ProviderName,
            runtime.ModelId,
            inputArray.Length);

        return generated.Select(embedding => embedding.Vector.ToArray()).ToArray();
    }
}
