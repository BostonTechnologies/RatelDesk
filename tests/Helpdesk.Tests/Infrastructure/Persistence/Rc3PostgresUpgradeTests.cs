using System.Text.Json;
using Helpdesk.API.Bootstrap;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Helpdesk.Tests.Infrastructure.Persistence;

public sealed class Rc3PostgresUpgradeTests
{
    [Fact]
    public async Task Populated_rc3_database_upgrades_repeatably_without_changing_tickets_or_oidc_identity()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg16").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), postgresOptions => postgresOptions.UseVector())
            // Match the production compatibility policy for historical PostgreSQL snapshots.
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new HelpdeskDbContext(options, new AdminTenantContext(), new HttpContextAccessor());
        var migrator = db.GetService<IMigrator>();
        const string rc3 = "20260910150846_AddAiAssistantChatTurnActivity";
        await migrator.MigrateAsync(rc3);
        var historicalMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var organization = new Organization { Id = "upgrade-org", Name = "Existing organization" };
        var user = new User { Id = "upgrade-admin", Name = "Existing administrator", Email = "admin@example.test", OrganizationId = organization.Id, Role = "HelpdeskAdmin" };
        var customer = new Customer { Id = "upgrade-customer", Name = "Existing customer", Email = "customer@example.test", OrganizationId = organization.Id };
        var incident = new Incident
        {
            Id = "upgrade-incident", TrackingId = "INC-UPGRADE", Title = "Existing incident", OrganizationId = organization.Id,
            CustomerId = customer.Id, AssignedToId = user.Id, Description = "Keep description", CcRecipients = ["listener@example.test"]
        };
        var request = new Request { Id = "upgrade-request", TrackingId = "REQ-UPGRADE", Title = "Existing request", OrganizationId = organization.Id, CustomerId = customer.Id };
        var change = new Change { Id = "upgrade-change", TrackingId = "CHG-UPGRADE", Title = "Existing change", OrganizationId = organization.Id, CustomerId = customer.Id };
        var task = new RequestTask { Id = "upgrade-task", TrackingId = "TASK-UPGRADE", RequestId = request.Id, OrganizationId = organization.Id, DueAt = DateTimeOffset.UtcNow.AddDays(1), Type = RequestTaskType.Manual };
        db.AddRange(organization, user, customer, incident, request, change, task);
        db.WorkLogs.Add(new WorkLog { Id = "upgrade-worklog", TicketId = incident.Id, TechnicianId = user.Id, NotesText = "Preserved private note", IsInternalNote = true, Hours = 0.5 });
        db.TicketTimelineEvents.Add(new TicketTimelineEvent { TicketId = incident.Id, CreatedByUserId = user.Id, EventType = TimelineEventType.TechnicianReply, MessageText = "Preserved reply" });
        db.Attachments.Add(new Attachment { Id = Guid.NewGuid(), TicketId = incident.Id, FileName = "evidence.txt", FilePath = "legacy/evidence.txt", ContentType = "text/plain", SizeBytes = 42, UploadedById = user.Id });
        await db.SaveChangesAsync();
        const string linkId = "upgrade-oidc-link";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CustomerAuthLinks" ("Id", "CustomerId", "AuthProviderType", "OidcIssuer", "OidcSubject", "InviteStatus")
            VALUES ({linkId}, {customer.Id}, {"Oidc"}, {"https://id.example.test"}, {"stable-subject"}, {(int)CustomerInviteStatus.Active});
            """);
        db.ChangeTracker.Clear();
        var before = await CaptureBusinessRowsAsync(db);

        await migrator.MigrateAsync();
        var adoption = new LegacyInstallationAdoptionService();
        Assert.Equal(LegacyInstallationAdoptionResult.Adopted, await adoption.AdoptAsync(db, CancellationToken.None));
        await migrator.MigrateAsync();
        Assert.Equal(LegacyInstallationAdoptionResult.AlreadyMarked, await adoption.AdoptAsync(db, CancellationToken.None));
        db.ChangeTracker.Clear();

        Assert.Equal(before, await CaptureBusinessRowsAsync(db));
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(historicalMigrations, applied.Take(historicalMigrations.Length));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var link = await db.CustomerAuthLinks.SingleAsync(x => x.Id == linkId);
        Assert.Equal(customer.Id, link.CustomerId);
        Assert.Equal("https://id.example.test", link.OidcIssuer);
        Assert.Equal("stable-subject", link.OidcSubject);
        Assert.Equal(CustomerInviteStatus.Active, link.InviteStatus);
        Assert.Null(link.LocalAccountId);
        Assert.Single(await db.InstanceInitializations.ToListAsync());
    }

    private static async Task<string> CaptureBusinessRowsAsync(HelpdeskDbContext db) => JsonSerializer.Serialize(new
    {
        Organizations = await db.Organizations.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Users = await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Customers = await db.Customers.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Incidents = await db.Incidents.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Requests = await db.Requests.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Changes = await db.Changes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Tasks = await db.RequestTasks.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Worklogs = await db.WorkLogs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Timeline = await db.TicketTimelineEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Attachments = await db.Attachments.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
    });

    private sealed class AdminTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;
    }
}
