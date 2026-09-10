using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Xunit;

namespace Helpdesk.Tests;

public class BlockedEntityRepositoryExtensionsTests
{
    [Fact]
    public async Task IsEmailBlockedAsync_ReturnsTrue_ForExactEmail()
    {
        var repo = new InMemoryRepository<BlockedEntity>();
        await repo.CreateAsync(new BlockedEntity { Email = "spam@example.com" });
        var result = await repo.IsEmailBlockedAsync("spam@example.com");
        Assert.True(result);
    }

    [Fact]
    public async Task IsEmailBlockedAsync_ReturnsTrue_WhenDomainBlocked()
    {
        var repo = new InMemoryRepository<BlockedEntity>();
        await repo.CreateAsync(new BlockedEntity { Domain = "baddomain.com" });
        var result = await repo.IsEmailBlockedAsync("user@baddomain.com");
        Assert.True(result);
    }

    [Fact]
    public async Task IsDomainBlockedAsync_ReturnsFalse_WhenNotBlocked()
    {
        var repo = new InMemoryRepository<BlockedEntity>();
        var result = await repo.IsDomainBlockedAsync("good.com");
        Assert.False(result);
    }
}
