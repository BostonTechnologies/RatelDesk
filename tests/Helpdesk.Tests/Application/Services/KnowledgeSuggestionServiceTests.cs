using Helpdesk.Application.Services.AI;
using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class KnowledgeSuggestionServiceTests
{
    [Fact]
    public async Task SuggestAsync_PersistsHighConfidenceResults()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());

        var retrieval = Substitute.For<IKnowledgeRetrievalService>();
        var article1 = new KnowledgeBaseArticle { Id = Guid.NewGuid(), Title = "A", OrganizationId = "org1" };
        var article2 = new KnowledgeBaseArticle { Id = Guid.NewGuid(), Title = "B", OrganizationId = "org1" };
        var article3 = new KnowledgeBaseArticle { Id = Guid.NewGuid(), Title = "C", OrganizationId = "org1" };
        ctx.KnowledgeBaseArticles.AddRange(article1, article2, article3);
        ctx.SaveChanges();

        var logger = Substitute.For<ILogger<KnowledgeSuggestionService>>();
        var service = new KnowledgeSuggestionService(ctx, retrieval, logger);

        var ticket = new Incident { Description = "test", OrganizationId = "org1" };

        retrieval.RetrieveAsync(ticket.OrganizationId, ticket.Description, 5, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new KnowledgeRetrievalResult(
                    article1,
                    0.9d,
                    [new KnowledgeEvidenceChunk(article1.Id, "0", article1.Id.ToString(), article1.Title, 0, "A", new Dictionary<string, string>(), 0.9d)],
                    true,
                    "High"),
                new KnowledgeRetrievalResult(
                    article2,
                    0.8d,
                    [new KnowledgeEvidenceChunk(article2.Id, "0", article2.Id.ToString(), article2.Title, 0, "B", new Dictionary<string, string>(), 0.8d)],
                    true,
                    "High")
            });

        var suggestions = (await service.SuggestAsync(ticket, CancellationToken.None)).ToList();

        await retrieval.Received(1).RetrieveAsync(ticket.OrganizationId, ticket.Description, 5, Arg.Any<CancellationToken>());
        Assert.Equal(2, suggestions.Count);
        Assert.Equal(2, ctx.TicketKnowledgeSuggestions.Count());
        Assert.All(suggestions, s => Assert.True(s.Score >= 0.5));
    }
}
