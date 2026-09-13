using System.Net;
using System.Net.Http.Json;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests.Api;

public sealed partial class LiveHistoryFilteringEndpointsTests
{
    [Theory]
    [InlineData("Incident", "assign")]
    [InlineData("Incident", "state")]
    [InlineData("Request", "assign")]
    [InlineData("Request", "state")]
    public async Task BulkWriter_CanUpdateItsTenantWithoutAdministratorRole(string module, string operation)
    {
        await using var harness = await LiveHistoryFilteringHarness.CreateAsync(ScopedTicketProfile(
            new(module + ".Read", "org-1"), new(module + ".Write", "org-1")));
        harness.UseRole(module + ".Write");
        await harness.SeedAsync(db =>
        {
            SeedScopedTickets(db);
            db.Users.Add(new User { Id = "assignee-a", Name = "Assignee A", Email = "assignee@example.test", OrganizationId = "org-1" });
        });
        var response = await harness.Client.PostAsJsonAsync($"/api/v1/{module.ToLowerInvariant()}s/bulk/{operation}", new
        {
            ids = new[] { module + "-a-peer" }, assignedToId = "assignee-a", newState = (int)TicketState.InProgress
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await harness.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.SingleAsync(item => item.Id == module + "-a-peer");
            if (operation == "assign") Assert.Equal("assignee-a", ticket.AssignedToId);
            else Assert.Equal(TicketState.InProgress, ticket.State);
            Assert.Equal(TicketState.New, (await db.Tickets.SingleAsync(item => item.Id == module + "-b-peer")).State);
        });
    }

    [Theory]
    [InlineData("Incident", "assign")]
    [InlineData("Incident", "state")]
    [InlineData("Request", "assign")]
    [InlineData("Request", "state")]
    public async Task BulkWriter_MixedTenantBatchIsDeniedBeforeAnyMutation(string module, string operation)
    {
        await using var harness = await LiveHistoryFilteringHarness.CreateAsync(ScopedTicketProfile(
            new(module + ".Read", "org-1"), new(module + ".Write", "org-1"), new(module + ".Read", "org-2")));
        harness.UseRole(module + ".Write");
        await harness.SeedAsync(SeedScopedTickets);
        var response = await harness.Client.PostAsJsonAsync($"/api/v1/{module.ToLowerInvariant()}s/bulk/{operation}", new
        {
            ids = new[] { module + "-a-peer", module + "-b-peer" }, assignedToId = "assignee-a", newState = (int)TicketState.InProgress
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertBulkTargetsUnchanged(harness, module);
    }

    [Theory]
    [InlineData("Incident", "assign")]
    [InlineData("Incident", "state")]
    [InlineData("Request", "assign")]
    [InlineData("Request", "state")]
    public async Task BulkReader_IsDeniedAndMissingTargetsDoNotPartiallyUpdate(string module, string operation)
    {
        await using var reader = await LiveHistoryFilteringHarness.CreateAsync(ScopedTicketProfile(new ScopedPermissionGrant(module + ".Read", "org-1")));
        reader.UseRole(module + ".Read");
        await reader.SeedAsync(SeedScopedTickets);
        var route = $"/api/v1/{module.ToLowerInvariant()}s/bulk/{operation}";
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.Client.PostAsJsonAsync(route, new
        {
            ids = new[] { module + "-a-peer" }, newState = (int)TicketState.InProgress
        })).StatusCode);
        await AssertBulkTargetsUnchanged(reader, module);

        await using var writer = await LiveHistoryFilteringHarness.CreateAsync(ScopedTicketProfile(
            new(module + ".Read", "org-1"), new(module + ".Write", "org-1")));
        writer.UseRole(module + ".Write");
        await writer.SeedAsync(SeedScopedTickets);
        Assert.Equal(HttpStatusCode.NotFound, (await writer.Client.PostAsJsonAsync(route, new
        {
            ids = new[] { module + "-a-peer", "missing-ticket" }, newState = (int)TicketState.InProgress
        })).StatusCode);
        await AssertBulkTargetsUnchanged(writer, module);
    }

    private static Task AssertBulkTargetsUnchanged(LiveHistoryFilteringHarness harness, string module) => harness.WithDbAsync(async db =>
    {
        var tickets = await db.Tickets.Where(ticket => ticket.Id == module + "-a-peer" || ticket.Id == module + "-b-peer").ToListAsync();
        Assert.All(tickets, ticket =>
        {
            Assert.Equal(TicketState.New, ticket.State);
            Assert.Null(ticket.AssignedToId);
        });
    });
}
