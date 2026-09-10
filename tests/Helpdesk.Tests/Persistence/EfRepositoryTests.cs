using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests.Persistence;

public sealed class EfRepositoryTests
{
    [Fact]
    public async Task GetAsync_ReturnsDerivedTask_ByExactStringId()
    {
        await using var db = CreateDb();
        var request = new Request { Id = Guid.NewGuid().ToString("N"), Title = "Request", Description = "Desc" };
        var task = new RequestTask
        {
            Id = Guid.NewGuid().ToString("N"),
            RequestId = request.Id,
            Title = "Task",
            Description = "Desc",
            Type = RequestTaskType.Automation
        };

        db.Requests.Add(request);
        db.RequestTasks.Add(task);
        await db.SaveChangesAsync();

        var repo = new EfRepository<RequestTask>(db);
        var loaded = await repo.GetAsync(task.Id);

        Assert.NotNull(loaded);
        Assert.Equal(task.Id, loaded!.Id);
        Assert.Equal(request.Id, loaded.RequestId);
    }

    [Fact]
    public async Task GetAsync_NormalizesDashedAndUndashedStringIds()
    {
        await using var db = CreateDb();
        var undashedId = Guid.NewGuid().ToString("N");
        var request = new Request
        {
            Id = undashedId,
            Title = "Request",
            Description = "Desc"
        };

        db.Requests.Add(request);
        await db.SaveChangesAsync();

        var repo = new EfRepository<Request>(db);
        var loaded = await repo.GetAsync(Guid.Parse(undashedId).ToString("D"));

        Assert.NotNull(loaded);
        Assert.Equal(undashedId, loaded!.Id);
    }

    [Fact]
    public async Task TicketQueryFilter_IncludesOwnAndManagedOrganizationTickets()
    {
        var tenant = new TestTenantContext
        {
            TenantIdValue = "msp-org",
            IsHelpdeskAdminValue = false
        };
        await using var db = CreateDb(tenant);
        db.Organizations.AddRange(
            new Organization { Id = "msp-org", Name = "MSP" },
            new Organization { Id = "managed-org", Name = "Managed", ItSupportOrganizationId = "msp-org" },
            new Organization { Id = "other-org", Name = "Other" });
        db.Changes.AddRange(
            new Change { Id = "own-change", Title = "Own", Description = "Own", OrganizationId = "msp-org" },
            new Change { Id = "managed-change", Title = "Managed", Description = "Managed", OrganizationId = "managed-org" },
            new Change { Id = "other-change", Title = "Other", Description = "Other", OrganizationId = "other-org" });
        await db.SaveChangesAsync();

        var visibleIds = await db.Changes
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(["managed-change", "own-change"], visibleIds);
    }

    private static HelpdeskDbContext CreateDb(TestTenantContext? tenant = null)
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HelpdeskDbContext(options, tenant ?? new TestTenantContext(), new HttpContextAccessor());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public string? TenantIdValue { get; init; }
        public bool IsHelpdeskAdminValue { get; init; } = true;
        public string? TenantId => TenantIdValue;
        public string? UserId => null;
        public bool IsHelpdeskAdmin => IsHelpdeskAdminValue;
    }
}
