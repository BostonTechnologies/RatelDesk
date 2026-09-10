using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.AI;
using Helpdesk.Infrastructure.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Pgvector;

namespace Helpdesk.Tests.Application.Services;

public class EmbeddingServiceTests
{
    private static EmbeddingService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler, out HelpdeskDbContext ctx)
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        var provider = new AiProvider
        {
            Id = Guid.NewGuid(),
            BaseUrl = "https://api.example.com/",
            ApiKeyEncrypted = "enc-key",
            DefaultModel = "text-embedding",
            IsEnabled = true
        };
        ctx.AiProviders.Add(provider);
        ctx.AiModels.Add(new AiModel { AiProviderId = provider.Id, Name = "text-embedding", IsEnabled = true });
        ctx.OrganizationAiKbSettings.Add(new OrganizationAiKbSettings
        {
            OrganizationId = "org1",
            EmbeddingProviderId = provider.Id.ToString(),
            EmbeddingModel = "text-embedding",
            EmbeddingDimensions = 512
        });
        ctx.SaveChanges();

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("OpenAICompatible")
               .Returns(new HttpClient(new TestHandler(handler)));

        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect("enc-key").Returns("plain");

        var runtime = Substitute.For<IAiRuntime>();
        var generatorLogger = Substitute.For<ILogger<LegacyEmbeddingGeneratorAdapter>>();
        runtime.CreateEmbeddingGeneratorAsync(Arg.Any<AiEmbeddingRuntimeRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<AiEmbeddingRuntimeRequest>();
                var generator = new LegacyEmbeddingGeneratorAdapter(
                    provider,
                    request.ModelId ?? "text-embedding",
                    request.Dimensions ?? 512,
                    protector,
                    factory,
                    generatorLogger);
                return new AiResolvedEmbeddingGenerator(generator, "provider", request.ModelId ?? "text-embedding", request.Dimensions ?? 512);
            });

        var logger = Substitute.For<ILogger<EmbeddingService>>();
        return new EmbeddingService(ctx, runtime, logger);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_ReturnsVector()
    {
        static HttpResponseMessage Response(HttpRequestMessage _)
        {
            var json = JsonSerializer.Serialize(new { embeddings = new[] { new { embedding = new[] { 0.1f, 0.2f } } } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        }

        var service = CreateService(Response, out _);

        var embedding = await service.CreateEmbeddingAsync("org1", "hello", CancellationToken.None);

        Assert.Equal(new[] { 0.1f, 0.2f }, embedding);
    }

    [Fact]
    public async Task VectorStore_SearchAsync_FiltersByOrganization_AndHonorsLimit()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());

        var a1 = new KnowledgeBaseArticle { OrganizationId = "org1", Service = "svc", Title = "a1" };
        var a2 = new KnowledgeBaseArticle { OrganizationId = "org1", Service = "svc", Title = "a2" };
        var b1 = new KnowledgeBaseArticle { OrganizationId = "org2", Service = "svc", Title = "b1" };
        ctx.KnowledgeBaseArticles.AddRange(a1, a2, b1);
        ctx.KnowledgeEmbeddings.AddRange(
            new KnowledgeEmbedding { OrganizationId = "org1", SourceType = "KB", SourceId = a1.Id.ToString(), DocumentTitle = a1.Title, ChunkIndex = 0, ChunkId = "0", MetadataJson = "{\"articleId\":\"" + a1.Id + "\"}", KnowledgeBaseArticleId = a1.Id, Text = "a1", Vector = new Vector(new[] { 1f, 0f }) },
            new KnowledgeEmbedding { OrganizationId = "org1", SourceType = "KB", SourceId = a2.Id.ToString(), DocumentTitle = a2.Title, ChunkIndex = 0, ChunkId = "0", MetadataJson = "{\"articleId\":\"" + a2.Id + "\"}", KnowledgeBaseArticleId = a2.Id, Text = "a2", Vector = new Vector(new[] { 0.9f, 0.1f }) },
            new KnowledgeEmbedding { OrganizationId = "org2", SourceType = "KB", SourceId = b1.Id.ToString(), DocumentTitle = b1.Title, ChunkIndex = 0, ChunkId = "0", MetadataJson = "{\"articleId\":\"" + b1.Id + "\"}", KnowledgeBaseArticleId = b1.Id, Text = "b1", Vector = new Vector(new[] { 1f, 0f }) }
        );
        ctx.SaveChanges();

        var store = new PgVectorKnowledgeVectorStore(ctx);

        var results = (await store.SearchAsync("org1", new[] { 1f, 0f }, 1, new KnowledgeVectorFilter(SourceTypes: ["KB"]), CancellationToken.None)).ToList();

        Assert.Single(results);
        Assert.Equal("org1", results[0].OrganizationId);
        Assert.Equal(a1.Id, results[0].KnowledgeBaseArticleId);
        Assert.Equal(a1.Title, results[0].DocumentTitle);
    }

    private class TestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
