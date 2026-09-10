using Helpdesk.Application.Services.Tickets;
using Xunit;

namespace Helpdesk.Tests.Application.Services;

public class DailySequenceTicketRefGeneratorTests
{
    [Fact]
    public async Task NextReferenceAsync_IncrementsPerDay()
    {
        var gen = new DailySequenceTicketRefGenerator();
        var first = await gen.NextReferenceAsync("INC");
        var second = await gen.NextReferenceAsync("INC");

        Assert.EndsWith("0001", first);
        Assert.EndsWith("0002", second);
        Assert.StartsWith("INC-", first);
    }
}
