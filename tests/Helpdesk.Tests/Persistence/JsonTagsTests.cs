using System.Text.Json;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Helpdesk.Tests.Persistence;

public class JsonTagsTests
{
    private class DummyTenantContext : ITenantContext
    {
        public string? TenantId => "tenant";
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;
    }

    [Fact]
    public async Task SaveChangesAsync_Persists_Tags_AsValidJson()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase("JsonTagsTest")
            .Options;

        await using var db = new HelpdeskDbContext(options, new DummyTenantContext(), new HttpContextAccessor());
        var article = new KnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            OrganizationId = "tenant",
            Service = "General",
            Title = "Article",
            Tags = ["alpha", "beta"]
        };

        await db.KnowledgeBaseArticles.AddAsync(article);
        var saved = await db.SaveChangesAsync();
        Assert.Equal(1, saved);

        var reloaded = await db.KnowledgeBaseArticles.FindAsync(article.Id);
        Assert.NotNull(reloaded);
        var json = JsonSerializer.Serialize(reloaded!.Tags);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Contains("alpha", reloaded.Tags);
        Assert.Contains("beta", reloaded.Tags);
    }
}

