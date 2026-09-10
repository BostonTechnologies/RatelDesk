using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class KnowledgeRetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_GroupsEvidenceByArticle_AndSetsConfidence()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());

        var article1 = new KnowledgeBaseArticle { Id = Guid.NewGuid(), OrganizationId = "org1", Service = "svc", Title = "Article 1" };
        var article2 = new KnowledgeBaseArticle { Id = Guid.NewGuid(), OrganizationId = "org1", Service = "svc", Title = "Article 2" };
        ctx.KnowledgeBaseArticles.AddRange(article1, article2);
        ctx.KnowledgeEmbeddings.AddRange(
            new KnowledgeEmbedding { OrganizationId = "org1", SourceType = "KB", KnowledgeBaseArticleId = article1.Id, SourceId = article1.Id.ToString(), DocumentTitle = article1.Title, ChunkIndex = 0, ChunkId = "0", Text = "Primary evidence", MetadataJson = "{\"articleId\":\"" + article1.Id + "\"}", Vector = new Pgvector.Vector(new[] { 1f, 0f }) },
            new KnowledgeEmbedding { OrganizationId = "org1", SourceType = "KB", KnowledgeBaseArticleId = article1.Id, SourceId = article1.Id.ToString(), DocumentTitle = article1.Title, ChunkIndex = 1, ChunkId = "1", Text = "Secondary evidence", MetadataJson = "{\"articleId\":\"" + article1.Id + "\"}", Vector = new Pgvector.Vector(new[] { 0.9f, 0.1f }) },
            new KnowledgeEmbedding { OrganizationId = "org1", SourceType = "KB", KnowledgeBaseArticleId = article2.Id, SourceId = article2.Id.ToString(), DocumentTitle = article2.Title, ChunkIndex = 0, ChunkId = "0", Text = "Other evidence", MetadataJson = "{\"articleId\":\"" + article2.Id + "\"}", Vector = new Pgvector.Vector(new[] { 0.6f, 0.4f }) });
        await ctx.SaveChangesAsync();

        var embeddings = Substitute.For<IEmbeddingService>();
        embeddings.CreateEmbeddingAsync("org1", "query", Arg.Any<CancellationToken>())
            .Returns(new[] { 1f, 0f });

        var store = new PgVectorKnowledgeVectorStore(ctx);
        var service = new KnowledgeRetrievalService(ctx, embeddings, store);

        var results = await service.RetrieveAsync("org1", "query", 2, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(article1.Id, results[0].Article.Id);
        Assert.True(results[0].IsHighConfidence);
        Assert.Equal("High", results[0].ConfidenceLabel);
        Assert.Equal(2, results[0].Evidence.Count);
        Assert.Equal("Primary evidence", results[0].Evidence[0].Text);
        Assert.Equal(article1.Title, results[0].Evidence[0].DocumentTitle);
        Assert.Equal(article1.Id.ToString(), results[0].Evidence[0].Metadata["articleId"]);
    }
}
