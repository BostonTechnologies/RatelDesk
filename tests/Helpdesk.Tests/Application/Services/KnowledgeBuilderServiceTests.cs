using Helpdesk.Application.Services.KB;
using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Helpdesk.Tests.Application.Services;

public class KnowledgeBuilderServiceTests
{
    [Fact]
    public async Task GenerateDraftFromResolvedTicketAsync_StoresLifecycleMetadataAndEmbeddings()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        ctx.AiProviders.Add(new AiProvider { Id = Guid.NewGuid(), BaseUrl = "https://api.example.com/", ApiKeyEncrypted = "key", DefaultModel = "model", IsEnabled = true });
        ctx.Incidents.Add(new Incident { Id = "t1", Title = "Issue", Description = "desc", State = TicketState.Resolved, OrganizationId = "org1" });
        ctx.WorkLogs.Add(new WorkLog { TicketId = "t1", Notes = "Worked", Hours = 1 });
        ctx.SaveChanges();

        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Title\nContent")));

        var aiRuntime = Substitute.For<IAiRuntime>();
        aiRuntime.CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiResolvedChatClient(chatClient, "provider", "model"));

        var embeddingService = Substitute.For<IEmbeddingService>();
        embeddingService.CreateEmbeddingsAsync("org1", Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float[]>>(new[] { new float[] { 0.1f } }));

        var vectorStore = new PgVectorKnowledgeVectorStore(ctx);
        var templates = Substitute.For<IAiPromptTemplateService>();
        templates.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string?>>())
            .Returns("Generate a concise knowledge base article. Output title on the first line, then body.");
        var audit = Substitute.For<IAiOperationAuditService>();
        var logger = Substitute.For<ILogger<KnowledgeBuilderService>>();
        var service = new KnowledgeBuilderService(aiRuntime, ctx, embeddingService, vectorStore, templates, audit, logger);

        var article = await service.GenerateDraftFromResolvedTicketAsync("t1", CancellationToken.None);

        Assert.NotNull(article);
        Assert.Equal("org1", article!.OrganizationId);
        Assert.Equal(KnowledgeBaseArticleState.Draft, article.State);
        Assert.Equal("t1", article.LinkedTicketId);
        Assert.Equal("model", article.CreatedByModel);
        Assert.NotNull(article.LastRegeneratedAt);
        Assert.True(article.CreatedAt <= DateTime.UtcNow);
        Assert.Single(ctx.KnowledgeBaseArticles);
        Assert.Single(ctx.KnowledgeEmbeddings);
        await embeddingService.Received(1)
            .CreateEmbeddingsAsync("org1", Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDraftFromResolvedTicketAsync_ReturnsExistingArticle()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        ctx.AiProviders.Add(new AiProvider { Id = Guid.NewGuid(), BaseUrl = "https://api.example.com/", ApiKeyEncrypted = "key", DefaultModel = "model", IsEnabled = true });
        ctx.Incidents.Add(new Incident { Id = "t1", Title = "Issue", Description = "desc", State = TicketState.Resolved, OrganizationId = "org1" });
        ctx.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle { LinkedTicketId = "t1", OrganizationId = "org1" });
        ctx.SaveChanges();

        var aiRuntime = Substitute.For<IAiRuntime>();
        var embeddingService = Substitute.For<IEmbeddingService>();
        var vectorStore = new PgVectorKnowledgeVectorStore(ctx);
        var templates = Substitute.For<IAiPromptTemplateService>();
        var audit = Substitute.For<IAiOperationAuditService>();
        var logger = Substitute.For<ILogger<KnowledgeBuilderService>>();
        var service = new KnowledgeBuilderService(aiRuntime, ctx, embeddingService, vectorStore, templates, audit, logger);

        var article = await service.GenerateDraftFromResolvedTicketAsync("t1", CancellationToken.None);

        Assert.NotNull(article);
        Assert.Equal("t1", article!.LinkedTicketId);
        await aiRuntime.DidNotReceive()
            .CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDraftFromResolvedTicketAsync_OnlyRunsForResolvedTickets()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        ctx.AiProviders.Add(new AiProvider { Id = Guid.NewGuid(), BaseUrl = "https://api.example.com/", ApiKeyEncrypted = "key", DefaultModel = "model", IsEnabled = true });
        ctx.Incidents.Add(new Incident { Id = "t1", Title = "Issue", Description = "desc", State = TicketState.New, OrganizationId = "org1" });
        ctx.SaveChanges();

        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Title\nContent")));
        var aiRuntime = Substitute.For<IAiRuntime>();
        aiRuntime.CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiResolvedChatClient(chatClient, "provider", "model"));
        var embeddingService = Substitute.For<IEmbeddingService>();
        embeddingService.CreateEmbeddingsAsync("org1", Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<float[]>>(new[] { new[] { 0.1f } }));
        var vectorStore = new PgVectorKnowledgeVectorStore(ctx);
        var templates = Substitute.For<IAiPromptTemplateService>();
        templates.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string?>>())
            .Returns("Generate a concise knowledge base article. Output title on the first line, then body.");
        var audit = Substitute.For<IAiOperationAuditService>();
        var logger = Substitute.For<ILogger<KnowledgeBuilderService>>();
        var service = new KnowledgeBuilderService(aiRuntime, ctx, embeddingService, vectorStore, templates, audit, logger);

        var before = await service.GenerateDraftFromResolvedTicketAsync("t1", CancellationToken.None);
        Assert.Null(before);
        await aiRuntime.DidNotReceive().CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>());

        var ticket = ctx.Incidents.First();
        ticket.State = TicketState.Resolved;
        ctx.SaveChanges();

        var after = await service.GenerateDraftFromResolvedTicketAsync("t1", CancellationToken.None);
        Assert.NotNull(after);
    }

}
