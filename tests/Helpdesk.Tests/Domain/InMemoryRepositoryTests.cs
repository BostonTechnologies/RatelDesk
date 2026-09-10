using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Xunit;

namespace Helpdesk.Tests;

public class InMemoryRepositoryTests
{
    [Fact]
    public async Task CreateAndGetAsync_Works()
    {
        var repo = new InMemoryRepository<Incident>();
        var incident = new Incident { Title = "Test", Description = "Desc" };

        var created = await repo.CreateAsync(incident);
        var fetched = await repo.GetAsync(created.Id);

        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedEntity()
    {
        var repo = new InMemoryRepository<Incident>();
        var incident = new Incident { Title = "Test" };
        await repo.CreateAsync(incident);

        incident.Title = "Updated";
        var updated = await repo.UpdateAsync(incident);

        Assert.Equal("Updated", updated!.Title);
    }
}
