using Helpdesk.Application.Requests;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Application.Requests;

public class CreateRequestCommandHandlerTests
{
    [Fact]
    public async Task Handle_Persists_AssignedToId_WhenProvided()
    {
        var repo = Substitute.For<IRepository<Request>>();
        Request? captured = null;
        repo.CreateAsync(Arg.Do<Request>(x => captured = x)).Returns(ci => ci.Arg<Request>());
        var handler = new CreateRequestCommandHandler(repo);
        var command = new CreateRequestCommand(
            "Test",
            "Desc",
            null,
            null,
            "org-1",
            null,
            null,
            null,
            "user-1");

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("user-1", captured!.AssignedToId);
    }
}
