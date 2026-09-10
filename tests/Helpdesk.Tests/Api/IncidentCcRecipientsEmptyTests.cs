using System.Net.Http.Json;
using Helpdesk.Shared.DTOs.Incident;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Api;

public partial class IncidentCcRecipientsEndpointsTests
{
    [Fact]
    public async Task Get_Returns_Empty_CcRecipients_When_None_Provided()
    {
        var incident = new Incident
        {
            Id = "inc-empty",
            Title = "Test",
            TrackingId = "INC-EMPTY",
            Priority = TicketPriority.Medium,
            State = TicketState.New
        };
        await _incidentRepo.CreateAsync(incident);

        var client = GetAuthenticatedClient();
        var dto = await client.GetFromJsonAsync<IncidentDto>($"/api/v1/incidents/{incident.Id}");

        Assert.NotNull(dto);
        Assert.Empty(dto!.CcRecipients);
    }
}

