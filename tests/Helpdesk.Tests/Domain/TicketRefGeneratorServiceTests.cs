using Helpdesk.Shared.Services;
using Xunit;

namespace Helpdesk.Tests;

public class TicketRefGeneratorServiceTests
{
    [Fact]
    public void GenerateIncidentRef_ReturnsExpectedFormat()
    {
        var service = new TicketRefGeneratorService();
        var ref1 = service.GenerateIncidentRef();
        var ref2 = service.GenerateIncidentRef();

        Assert.Matches("^INC-\\d{6}$", ref1);
        Assert.Matches("^INC-\\d{6}$", ref2);
        Assert.NotEqual(ref1, ref2);
    }
}
