using Helpdesk.Application.RequestTasks;
using Helpdesk.Infrastructure.Orchestration;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests.Infrastructure.Orchestration;

public sealed class AutomationBindingServiceTests
{
    [Fact]
    public async Task ListAsync_OrdersBindingsByCreationTime_WithSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new HelpdeskDbContext(options, new TestTenantContext(), new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();

        db.RequestForms.Add(new RequestForm
        {
            Id = "form-1",
            OrganizationId = "org-1",
            Title = "Request form"
        });
        db.AutomationBindings.AddRange(
            CreateBinding(
                "binding-new",
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DateTimeOffset.Parse("2026-01-02T00:00:00+00:00")),
            CreateBinding(
                "binding-old",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                DateTimeOffset.Parse("2026-01-01T00:00:00+00:00")));
        await db.SaveChangesAsync();

        var service = new AutomationBindingService(db, new TestTenantContext(), new RequestFormSchemaParser());

        var bindings = await service.ListAsync("form-1");

        Assert.Equal(["binding-old", "binding-new"], bindings.Select(binding => binding.Id));
    }

    private static AutomationBinding CreateBinding(
        string id,
        Guid taskTemplateId,
        DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        OrganizationId = "org-1",
        RequestFormId = "form-1",
        TaskTemplateId = taskTemplateId,
        OrchestrationRequestDefinitionId = "orchestration-request",
        CreatedAtUtc = createdAtUtc,
        UpdatedAtUtc = createdAtUtc
    };

    private sealed class TestTenantContext : ITenantContext
    {
        public string? TenantId => "org-1";
        public string? UserId => "user-1";
        public bool IsHelpdeskAdmin => false;
    }
}
