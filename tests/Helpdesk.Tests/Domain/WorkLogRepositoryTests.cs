using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Xunit;
using Dodo.Primitives;

namespace Helpdesk.Tests;

public class WorkLogRepositoryTests
{
    [Fact]
    public async Task CanCreateAndRetrieveWorkLog()
    {
        var repo = new InMemoryRepository<WorkLog>();
        var log = new WorkLog { TicketId = Uuid.CreateVersion7().ToString(), Hours = 2, Notes = "Initial work" };

        await repo.CreateAsync(log);
        var fetched = await repo.GetAsync(log.Id);

        Assert.NotNull(fetched);
        Assert.Equal(log.Notes, fetched!.Notes);
    }
}
