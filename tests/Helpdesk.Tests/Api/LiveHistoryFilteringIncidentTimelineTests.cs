using System.Net;
using System.Net.Http.Json;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Worklog;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Tests.Api;

public sealed partial class LiveHistoryFilteringEndpointsTests
{
    [Theory]
    [InlineData("asc", "older", "newer")]
    [InlineData("desc", "newer", "older")]
    public async Task IncidentTimeline_SqliteOrdersUtcInstantsWithinAuthorizedTicket(string order, string first, string last)
    {
        var grants = new HashSet<ScopedPermissionGrant> { new(HelpdeskPermissions.IncidentRead, "org-1") };
        var access = new CurrentUserAccessProfile(true, "Reader", null, "org-1", null, null, false,
            new HashSet<string>(), new HashSet<string> { HelpdeskPermissions.IncidentRead },
            new HashSet<string> { "org-1" }, new HashSet<string>()) { ScopedPermissionGrants = grants };
        await using var harness = await LiveHistoryFilteringHarness.CreateAsync(access);
        await harness.SeedAsync(db =>
        {
            db.Incidents.AddRange(
                new Incident { Id = "incident-read", TrackingId = "INC-READ", Title = "Readable", OrganizationId = "org-1" },
                new Incident { Id = "incident-other", TrackingId = "INC-OTHER", Title = "Foreign", OrganizationId = "org-2" });
            db.TicketTimelineEvents.AddRange(
                new TicketTimelineEvent { TicketId = "incident-read", CreatedUtc = DateTimeOffset.Parse("2026-01-01T09:00:00+02:00"), MessageText = "older", EventType = TimelineEventType.InternalNote },
                new TicketTimelineEvent { TicketId = "incident-read", CreatedUtc = DateTimeOffset.Parse("2026-01-01T08:00:00Z"), MessageText = "newer", EventType = TimelineEventType.InternalNote },
                new TicketTimelineEvent { TicketId = "incident-other", MessageText = "foreign", EventType = TimelineEventType.InternalNote });
        });
        var timeline = await harness.Client.GetFromJsonAsync<List<TicketTimelineEventDto>>($"/api/v1/incidents/incident-read/timeline?order={order}");
        Assert.Equal(new[] { first, last }, timeline!.Select(evt => evt.MessageText));
        var denied = await harness.Client.GetAsync("/api/v1/incidents/incident-other/timeline");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
