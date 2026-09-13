using System.Net;
using System.Net.Http.Json;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.Models;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public sealed partial class LiveHistoryFilteringEndpointsTests
{
    [Fact]
    public async Task RequestWriterCannotExecuteAutomationOrOverwriteTaskRuntimeThroughMetadataUpdate()
    {
        var lifecycle = Substitute.For<IRequestTaskLifecycleService>();
        var access = ScopedTicketProfile(new(HelpdeskPermissions.RequestRead, "org-1"), new(HelpdeskPermissions.RequestWrite, "org-1"));
        await using var harness = await LiveHistoryFilteringHarness.CreateAsync(access, lifecycle);
        await SeedExecutionTasksAsync(harness);
        foreach (var action in new[] { "start", "complete", "fail", "retry" })
        {
            var denied = await harness.Client.PostAsync($"/api/v1/request-tasks/automation-1/{action}", null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var deletion = await harness.Client.DeleteAsync("/api/v1/request-tasks/automation-1");
        Assert.Equal(HttpStatusCode.Forbidden, deletion.StatusCode);
        var update = await harness.Client.PutAsJsonAsync("/api/v1/request-tasks/automation-1", new RequestTask
        {
            Title = "Updated metadata", OrganizationId = "org-1", RequestId = "execution-request-1",
            Type = RequestTaskType.Manual, Status = RequestTaskStatus.Completed, AutomationBindingId = "attacker-binding"
        });
        update.EnsureSuccessStatusCode();
        await harness.WithDbAsync(async db =>
        {
            var task = await db.RequestTasks.FindAsync("automation-1");
            Assert.Equal("Updated metadata", task!.Title);
            Assert.Equal(RequestTaskType.Automation, task.Type);
            Assert.Equal(RequestTaskStatus.Pending, task.Status);
            Assert.Null(task.AutomationBindingId);
        });
        Assert.Empty(lifecycle.ReceivedCalls());
    }

    [Fact]
    public async Task ExecutorCanStartOnlyAutomationInItsGrantedTenantIncludingBulkRequests()
    {
        var lifecycle = Substitute.For<IRequestTaskLifecycleService>();
        lifecycle.StartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => new RequestTask { Id = call.ArgAt<string>(0), Type = RequestTaskType.Automation });
        var access = ScopedTicketProfile(new(HelpdeskPermissions.RequestRead, "org-1"), new(HelpdeskPermissions.RequestExecute, "org-1"));
        await using var harness = await LiveHistoryFilteringHarness.CreateAsync(access, lifecycle);
        await SeedExecutionTasksAsync(harness);
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.GetAsync("/api/v1/request-tasks/automation-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.PostAsync("/api/v1/request-tasks/automation-1/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await harness.Client.PostAsync("/api/v1/request-tasks/automation-2/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await harness.Client.PostAsync("/api/v1/request-tasks/manual-1/start", null)).StatusCode);
        var bulk = await harness.Client.PostAsJsonAsync("/api/v1/request-tasks/bulk/start",
            new BulkRequestTaskActionDto { Ids = ["automation-1", "automation-2", "manual-1"] });
        bulk.EnsureSuccessStatusCode();
        await lifecycle.Received(2).StartAsync("automation-1", Arg.Any<CancellationToken>());
        await lifecycle.DidNotReceive().StartAsync("automation-2", Arg.Any<CancellationToken>());
        await lifecycle.DidNotReceive().StartAsync("manual-1", Arg.Any<CancellationToken>());
    }

    private static Task SeedExecutionTasksAsync(LiveHistoryFilteringHarness harness) => harness.SeedAsync(db =>
    {
        db.Requests.AddRange(
            new Request { Id = "execution-request-1", TrackingId = "REQ-EXEC-1", Title = "Allowed", OrganizationId = "org-1" },
            new Request { Id = "execution-request-2", TrackingId = "REQ-EXEC-2", Title = "Other", OrganizationId = "org-2" });
        db.RequestTasks.AddRange(
            new RequestTask { Id = "automation-1", TrackingId = "TASK-AUTO-1", Title = "Automation", RequestId = "execution-request-1", OrganizationId = "org-1", Type = RequestTaskType.Automation },
            new RequestTask { Id = "automation-2", TrackingId = "TASK-AUTO-2", Title = "Other", RequestId = "execution-request-2", OrganizationId = "org-2", Type = RequestTaskType.Automation },
            new RequestTask { Id = "manual-1", TrackingId = "TASK-MANUAL-1", Title = "Manual", RequestId = "execution-request-1", OrganizationId = "org-1", Type = RequestTaskType.Manual });
    });
}
