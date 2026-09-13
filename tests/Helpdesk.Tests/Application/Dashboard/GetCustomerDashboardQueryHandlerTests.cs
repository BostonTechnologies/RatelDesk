using Helpdesk.Application.Dashboard;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;

namespace Helpdesk.Tests.Application.Dashboard;

public sealed class GetCustomerDashboardQueryHandlerTests
{
    [Fact]
    public async Task CountsRequireOwnIncidentPermissionInTheTicketsCurrentOrganization()
    {
        var repository = Substitute.For<IRepository<Incident>>();
        repository.GetAllAsync().Returns(new[]
        {
            new Incident { Id = "visible", CustomerId = "customer", OrganizationId = "allowed" },
            new Incident { Id = "moved", CustomerId = "customer", OrganizationId = "revoked" },
            new Incident { Id = "other-customer", CustomerId = "another", OrganizationId = "allowed" }
        });
        var handler = new GetCustomerDashboardQueryHandler(repository);
        var allowed = await handler.Handle(new GetCustomerDashboardQuery("customer", new HashSet<string> { "allowed" }), CancellationToken.None);
        Assert.Equal(1, allowed.OpenTicketsCount);
        var removed = await handler.Handle(new GetCustomerDashboardQuery("customer", new HashSet<string>()), CancellationToken.None);
        Assert.Equal(0, removed.OpenTicketsCount);
    }
}
