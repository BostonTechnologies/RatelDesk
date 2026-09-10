using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class RequesterReplyDraftServiceTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsClarificationDraft_ForLowConfidenceMatch()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        ctx.OrganizationAiKbSettings.Add(new OrganizationAiKbSettings
        {
            OrganizationId = "org1",
            EnableAiSearch = true,
            EnableAiAnswers = true
        });
        await ctx.SaveChangesAsync();

        var retrieval = Substitute.For<IKnowledgeRetrievalService>();
        var article = new KnowledgeBaseArticle { Id = Guid.NewGuid(), Title = "VPN Reset", OrganizationId = "org1" };
        retrieval.RetrieveAsync("org1", "vpn broken", 1, Arg.Any<CancellationToken>())
            .Returns(
            [
                new KnowledgeRetrievalResult(
                    article,
                    0.55d,
                    [new KnowledgeEvidenceChunk(article.Id, "0", article.Id.ToString(), article.Title, 0, "VPN evidence", new Dictionary<string, string>(), 0.55d)],
                    false,
                    "Low")
            ]);

        var aiRuntime = Substitute.For<IAiRuntime>();
        var prompts = Substitute.For<IAiPromptTemplateService>();
        var audit = Substitute.For<IAiOperationAuditService>();
        var logger = Substitute.For<ILogger<RequesterReplyDraftService>>();
        var service = new RequesterReplyDraftService(aiRuntime, retrieval, prompts, audit, ctx, logger);

        var draft = await service.GenerateAsync(
            new Incident { Id = "t1", OrganizationId = "org1", Title = "VPN", Description = "vpn broken" },
            CancellationToken.None);

        Assert.NotNull(draft);
        Assert.True(draft!.RequiresClarification);
        Assert.Equal("Low", draft.ConfidenceLabel);
        Assert.Equal(3, draft.FollowUpQuestions.Count);
        await aiRuntime.DidNotReceive().CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_UsesAiRuntime_ForHighConfidenceMatch()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        await using var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        ctx.OrganizationAiKbSettings.Add(new OrganizationAiKbSettings
        {
            OrganizationId = "org1",
            EnableAiSearch = true,
            EnableAiAnswers = true
        });
        await ctx.SaveChangesAsync();

        var retrieval = Substitute.For<IKnowledgeRetrievalService>();
        var article = new KnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            Title = "Password Reset",
            OrganizationId = "org1",
            Summary = "Reset your password from the portal.",
            Resolution = "Use the self-service reset link."
        };
        retrieval.RetrieveAsync("org1", "password issue", 1, Arg.Any<CancellationToken>())
            .Returns(
            [
                new KnowledgeRetrievalResult(
                    article,
                    0.92d,
                    [new KnowledgeEvidenceChunk(article.Id, "0", article.Id.ToString(), article.Title, 0, "Reset link evidence", new Dictionary<string, string>(), 0.92d)],
                    true,
                    "High")
            ]);

        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Please use the self-service password reset link and let us know if it fails.")));

        var aiRuntime = Substitute.For<IAiRuntime>();
        aiRuntime.CreateChatClientAsync(Arg.Any<AiChatRuntimeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiResolvedChatClient(chatClient, "provider", "model"));

        var prompts = Substitute.For<IAiPromptTemplateService>();
        prompts.Render("Ticket.RequesterReplyDraft.System", Arg.Any<IReadOnlyDictionary<string, string?>>())
            .Returns("Draft a concise requester response.");
        var audit = Substitute.For<IAiOperationAuditService>();
        var logger = Substitute.For<ILogger<RequesterReplyDraftService>>();
        var service = new RequesterReplyDraftService(aiRuntime, retrieval, prompts, audit, ctx, logger);

        var draft = await service.GenerateAsync(
            new Incident { Id = "t2", OrganizationId = "org1", Title = "Password", Description = "password issue" },
            CancellationToken.None);

        Assert.NotNull(draft);
        Assert.False(draft!.RequiresClarification);
        Assert.Equal("High", draft.ConfidenceLabel);
        Assert.Contains("self-service password reset", draft.DraftReply, StringComparison.OrdinalIgnoreCase);
        await audit.Received(1).RecordAsync(
            Arg.Is<AiOperationAuditEntry>(entry => entry.OperationName == "ticket-requester-reply" && entry.SubjectId == "t2"),
            Arg.Any<CancellationToken>());
    }
}
