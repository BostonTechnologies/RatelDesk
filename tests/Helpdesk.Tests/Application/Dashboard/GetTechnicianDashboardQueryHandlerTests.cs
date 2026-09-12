using Helpdesk.Application.Dashboard;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Tests.Application.Dashboard;

public sealed class GetTechnicianDashboardQueryHandlerTests
{
    [Fact]
    public async Task Handle_FiltersPendingChangesToTheCallersPermittedOrganizations()
    {
        var incidents = new InMemoryRepository<Incident>();
        var changes = new InMemoryRepository<Change>();
        await incidents.CreateAsync(new Incident
        {
            Id = "incident-assigned",
            Title = "Assigned incident",
            AssignedToId = "technician-1",
            State = TicketState.InProgress
        });
        await incidents.CreateAsync(new Incident
        {
            Id = "incident-unassigned",
            Title = "Other incident",
            AssignedToId = "technician-2",
            State = TicketState.InProgress
        });
        await changes.CreateAsync(new Change
        {
            Id = "change-alpha",
            Title = "Alpha change",
            OrganizationId = "org-alpha",
            State = TicketState.InProgress
        });
        await changes.CreateAsync(new Change
        {
            Id = "change-other",
            Title = "Other change",
            OrganizationId = "org-other",
            State = TicketState.InProgress
        });

        var handler = new GetTechnicianDashboardQueryHandler(incidents, changes);

        var result = await handler.Handle(
            new GetTechnicianDashboardQuery(
                "technician-1",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "org-alpha" },
                IsHelpdeskAdmin: false),
            CancellationToken.None);

        Assert.Equal(1, result.OpenIncidentsCount);
        Assert.Equal(1, result.PendingChangesCount);
    }

    [Fact]
    public async Task Handle_IncludesAllPendingChangesForAnInstanceAdministrator()
    {
        var incidents = new InMemoryRepository<Incident>();
        var changes = new InMemoryRepository<Change>();
        await changes.CreateAsync(new Change { Id = "change-alpha", Title = "Alpha change", OrganizationId = "org-alpha", State = TicketState.InProgress });
        await changes.CreateAsync(new Change { Id = "change-other", Title = "Other change", OrganizationId = "org-other", State = TicketState.InProgress });
        var handler = new GetTechnicianDashboardQueryHandler(incidents, changes);

        var result = await handler.Handle(
            new GetTechnicianDashboardQuery(
                "administrator",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                IsHelpdeskAdmin: true),
            CancellationToken.None);

        Assert.Equal(2, result.PendingChangesCount);
    }
}
