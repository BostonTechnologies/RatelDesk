using Helpdesk.API.Background;
using Helpdesk.Application.RequestTasks;
using Helpdesk.Application.Services.Notifications;
using Helpdesk.Application.Sla;
using Helpdesk.Application.Tickets;
using Helpdesk.Application.Timeline;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.RequestTasks;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure.Persistence;

public sealed class SqliteProviderRegressionTests
{
    [Fact]
    public async Task Audit_migration_preserves_existing_rows_and_pages_by_instant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260912220557_AddExternalIdentityDomainUserLink");
        var earlier = Guid.NewGuid();
        var later = Guid.NewGuid();
        // Local clock order is reversed; ordering must compare UTC instants.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO AiOperationAuditRecords (Id, OperationName, ProviderName, ModelId, CreatedAt) VALUES ({earlier}, {"a"}, {"provider"}, {"model"}, {"2026-09-13 12:00:00+02:00"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO AiOperationAuditRecords (Id, OperationName, ProviderName, ModelId, CreatedAt) VALUES ({later}, {"b"}, {"provider"}, {"model"}, {"2026-09-13 06:01:00-04:00"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO AutomationBindings (Id, OrganizationId, RequestFormId, TaskTemplateId, OrchestrationRequestDefinitionId, SyncState, Enabled, CreatedAtUtc, UpdatedAtUtc) VALUES ({"existing-binding"}, {"org"}, {"form"}, {Guid.NewGuid()}, {"definition"}, {0}, {false}, {"2026-09-13 12:00:00+02:00"}, {"2026-09-13 12:00:00+02:00"})");

        await migrator.MigrateAsync();
        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var query = db.AiOperationAuditRecords.OrderByUtc(db, x => x.CreatedAt, descending: true).Take(1);
        Assert.Contains("LIMIT", query.ToQueryString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(later, (await query.SingleAsync()).Id);
        Assert.Equal(earlier, (await db.AiOperationAuditRecords.OrderByUtc(db, x => x.CreatedAt).Take(1).SingleAsync()).Id);
        Assert.Equal(2, await db.AiOperationAuditRecords.CountAsync());
        Assert.Equal("existing-binding", (await db.AutomationBindings.OrderByUtc(db, x => x.UpdatedAtUtc).Take(1).SingleAsync()).Id);

        // Cover every new sortable mapping through a translated query on the real schema.
        Assert.Empty(await db.InboundEmailProcessingLogs.OrderByUtc(db, x => x.CreatedAtUtc).Take(1).ToListAsync());
        Assert.Empty(await db.DatasetIngestCredentials.OrderByUtc(db, x => x.CreatedAtUtc).Take(1).ToListAsync());
        Assert.Empty(await db.TicketAiFeedback.OrderByUtc(db, x => x.CreatedAt).Take(1).ToListAsync());
        Assert.Empty(await db.AiInvestigationWorklogEntries.OrderByUtc(db, x => x.OccurredUtc).Take(1).ToListAsync());
    }

    [Fact]
    public async Task Approval_timeout_job_executes_on_migrated_sqlite_and_only_expires_due_approvals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var due = AddApproval(db, "due", now.AddHours(-1).ToOffset(TimeSpan.FromHours(2)));
        var future = AddApproval(db, "future", now.AddHours(1).ToOffset(TimeSpan.FromHours(-4)));
        var withoutDate = AddApproval(db, "no-date", null);
        await db.SaveChangesAsync();
        var service = new RequestTaskApprovalService(db, new RequestFormSchemaParser(),
            Substitute.For<ITicketNotificationService>(), Substitute.For<IPublicTicketLinkSigner>(),
            Substitute.For<ITimelineEventBus>(), NullLogger<RequestTaskApprovalService>.Instance);
        var job = new RequestTaskApprovalTimeoutHangfireJob(new RequestTaskApprovalTimeoutProcessor(service));

        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        Assert.Equal(RequestTaskApprovalStatus.Expired, (await db.RequestTaskApprovals.SingleAsync(x => x.Id == due)).Status);
        Assert.Equal(RequestTaskApprovalStatus.Pending, (await db.RequestTaskApprovals.SingleAsync(x => x.Id == future)).Status);
        Assert.Equal(RequestTaskApprovalStatus.Pending, (await db.RequestTaskApprovals.SingleAsync(x => x.Id == withoutDate)).Status);
        Assert.Equal("Cancelled", (await db.Requests.SingleAsync(x => x.Id == "request-due")).WorkflowStatus);
        Assert.Equal(RequestTaskStatus.Cancelled, (await db.RequestTasks.SingleAsync(x => x.Id == "task-due")).Status);
        Assert.Equal(RequestTaskStatus.PendingApproval, (await db.RequestTasks.SingleAsync(x => x.Id == "task-future")).Status);
    }

    [Fact]
    public async Task Sla_evaluation_job_persists_breach_state_on_migrated_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.MigrateAsync();
        var start = DateTimeOffset.UtcNow.AddHours(-10);
        db.Incidents.Add(new Incident { Id = "sla-ticket", TrackingId = "INC-SLA", Title = "Overdue" });
        db.TicketSlaStates.Add(new TicketSlaState
        {
            TicketId = "sla-ticket", StartedAt = start, ResponseDueAt = start.AddHours(1),
            ResolutionDueAt = start.AddHours(2), Status = SlaStatus.InProgress
        });
        await db.SaveChangesAsync();
        var job = new SlaEvaluationHangfireJob(new SlaEvaluationJob(
            new TicketSlaQueryRepository(db), new TicketSlaRepository(db), new SlaClockService(),
            Substitute.For<ISlaEscalationEvaluator>(), Options.Create(new SlaEvaluationJobSettings { BatchSize = 1 }),
            NullLogger<SlaEvaluationJob>.Instance));

        await job.RunAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        var state = await db.TicketSlaStates.SingleAsync();
        Assert.True(state.ResponseBreached);
        Assert.True(state.ResolutionBreached);
        Assert.Equal(SlaStatus.Breached, state.Status);
    }

    private static string AddApproval(HelpdeskDbContext db, string name, DateTimeOffset? dueAt)
    {
        var request = new Request { Id = $"request-{name}", TrackingId = $"REQ-{name}", State = TicketState.PendingApproval };
        var task = new RequestTask
        {
            Id = $"task-{name}", TrackingId = $"TASK-{name}", Request = request,
            RequestId = request.Id, Type = RequestTaskType.Approval,
            Status = RequestTaskStatus.PendingApproval, DueAt = dueAt
        };
        var approval = new RequestTaskApproval
        {
            Request = request, RequestId = request.Id, RequestTask = task, RequestTaskId = task.Id,
            ApproverEmail = "approver@example.com", Status = RequestTaskApprovalStatus.Pending,
            TokenSentAtUtc = DateTimeOffset.UtcNow
        };
        db.RequestTaskApprovals.Add(approval);
        return approval.Id;
    }

    private static HelpdeskDbContext CreateDb(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly("Helpdesk.Infrastructure.SqliteMigrations"))
            .Options,
        new AdminTenantContext(), new HttpContextAccessor());

    private sealed class AdminTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;
    }
}
