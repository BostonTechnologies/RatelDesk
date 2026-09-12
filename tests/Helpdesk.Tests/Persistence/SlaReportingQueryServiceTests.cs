using Helpdesk.Application.Sla;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Persistence;

public class SlaReportingQueryServiceTests
{
    [Fact]
    public async Task GetComplianceSummaryAsync_ReturnsExpectedCountsAndPercentages()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        db.Tickets.AddRange(
            new Incident { Id = "c1", Title = "C1", TrackingId = "INC-1", OrganizationId = "tenant-a", State = TicketState.Resolved },
            new Incident { Id = "c2", Title = "C2", TrackingId = "INC-2", OrganizationId = "tenant-a", State = TicketState.Resolved },
            new Request { Id = "c3", Title = "C3", TrackingId = "REQ-1", OrganizationId = "tenant-a", State = TicketState.Resolved });

        db.TicketSlaStates.AddRange(
            new TicketSlaState { TicketId = "c1", Status = SlaStatus.Completed, CompletedAt = now.AddDays(-1), CompletedWithinResolutionSla = true, CompletedWithinResponseSla = true },
            new TicketSlaState { TicketId = "c2", Status = SlaStatus.Completed, CompletedAt = now.AddDays(-1), CompletedWithinResolutionSla = false, CompletedWithinResponseSla = true },
            new TicketSlaState { TicketId = "c3", Status = SlaStatus.Completed, CompletedAt = now.AddDays(-2), CompletedWithinResolutionSla = true, CompletedWithinResponseSla = false });

        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.GetComplianceSummaryAsync(new SlaComplianceQuery
        {
            TenantId = "tenant-a",
            FromUtc = now.AddDays(-7),
            ToUtc = now
        }, CancellationToken.None);

        Assert.Equal(3, result.CompletedTotal);
        Assert.Equal(2, result.CompletedWithinResolutionSla);
        Assert.Equal(1, result.CompletedBreachedResolutionSla);
        Assert.Equal(2, result.CompletedWithinResponseSla);
        Assert.Equal(1, result.CompletedBreachedResponseSla);
        Assert.Equal(66.67, result.ResolutionCompliancePercent);
        Assert.Equal(66.67, result.ResponseCompliancePercent);
        Assert.NotEmpty(result.Buckets);
    }

    [Fact]
    public async Task GetCompletedTicketsAsync_FiltersByWithinResolutionSla()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        db.Tickets.AddRange(
            new Incident { Id = "k1", Title = "K1", TrackingId = "INC-1", OrganizationId = "tenant-a", State = TicketState.Resolved },
            new Incident { Id = "k2", Title = "K2", TrackingId = "INC-2", OrganizationId = "tenant-a", State = TicketState.Resolved });

        db.TicketSlaStates.AddRange(
            new TicketSlaState { TicketId = "k1", Status = SlaStatus.Completed, CompletedAt = now.AddHours(-2), CompletedWithinResolutionSla = true, CompletedWithinResponseSla = true },
            new TicketSlaState { TicketId = "k2", Status = SlaStatus.Completed, CompletedAt = now.AddHours(-1), CompletedWithinResolutionSla = false, CompletedWithinResponseSla = true });

        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.GetCompletedTicketsAsync(new SlaCompletedQuery
        {
            TenantId = "tenant-a",
            FromUtc = now.AddDays(-1),
            ToUtc = now,
            WithinResolutionSla = true,
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("k1", result.Items[0].TicketId);
    }

    [Fact]
    public async Task GetBreachedTicketsAsync_PaginatesResults()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        db.Tickets.AddRange(
            new Incident { Id = "b1", Title = "B1", TrackingId = "INC-B1", OrganizationId = "tenant-a", State = TicketState.InProgress },
            new Incident { Id = "b2", Title = "B2", TrackingId = "INC-B2", OrganizationId = "tenant-a", State = TicketState.InProgress },
            new Incident { Id = "b3", Title = "B3", TrackingId = "INC-B3", OrganizationId = "tenant-a", State = TicketState.InProgress });

        db.TicketSlaStates.AddRange(
            CreateBreachedState("b1", now.AddHours(-4)),
            CreateBreachedState("b2", now.AddHours(-3)),
            CreateBreachedState("b3", now.AddHours(-2)));

        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.GetBreachedTicketsAsync(new SlaTicketListQuery
        {
            TenantId = "tenant-a",
            Page = 1,
            PageSize = 2
        }, CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetNearBreachTicketsAsync_FiltersByThreshold()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        db.Tickets.AddRange(
            new Request { Id = "n1", Title = "Near", TrackingId = "REQ-N1", OrganizationId = "tenant-a", State = TicketState.InProgress },
            new Request { Id = "n2", Title = "Low", TrackingId = "REQ-N2", OrganizationId = "tenant-a", State = TicketState.InProgress });

        db.TicketSlaStates.AddRange(
            new TicketSlaState
            {
                TicketId = "n1",
                StartedAt = now.AddMinutes(-90),
                ResponseDueAt = now.AddMinutes(30),
                ResolutionDueAt = now.AddMinutes(10),
                Status = SlaStatus.InProgress
            },
            new TicketSlaState
            {
                TicketId = "n2",
                StartedAt = now.AddMinutes(-30),
                ResponseDueAt = now.AddMinutes(90),
                ResolutionDueAt = now.AddMinutes(90),
                Status = SlaStatus.InProgress
            });

        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.GetNearBreachTicketsAsync(new SlaNearBreachQuery
        {
            TenantId = "tenant-a",
            Metric = SlaMetricType.Resolution,
            ThresholdPercent = 80,
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("n1", result.Items[0].TicketId);
    }

    [Fact]
    public async Task Sqlite_provider_executes_sla_datetime_filters_and_ordering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new HelpdeskDbContext(options, new TestTenantContext(), new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;

        db.Tickets.AddRange(
            new Incident { Id = "sqlite-completed-old", Title = "Old", TrackingId = "INC-S1", OrganizationId = "tenant-a", State = TicketState.Resolved },
            new Incident { Id = "sqlite-completed-new", Title = "New", TrackingId = "INC-S2", OrganizationId = "tenant-a", State = TicketState.Resolved },
            new Incident { Id = "sqlite-breached-late", Title = "Late", TrackingId = "INC-S3", OrganizationId = "tenant-a", State = TicketState.InProgress },
            new Incident { Id = "sqlite-breached-early", Title = "Early", TrackingId = "INC-S4", OrganizationId = "tenant-a", State = TicketState.InProgress });
        db.TicketSlaStates.AddRange(
            new TicketSlaState { TicketId = "sqlite-completed-old", Status = SlaStatus.Completed, CompletedAt = now.AddHours(-2), CompletedWithinResolutionSla = true },
            new TicketSlaState { TicketId = "sqlite-completed-new", Status = SlaStatus.Completed, CompletedAt = now.AddHours(-1), CompletedWithinResolutionSla = false },
            CreateBreachedState("sqlite-breached-late", now.AddHours(-1)),
            CreateBreachedState("sqlite-breached-early", now.AddHours(-2)));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var completed = await sut.GetCompletedTicketsAsync(new SlaCompletedQuery
        {
            TenantId = "tenant-a", FromUtc = now.AddDays(-1), ToUtc = now, Page = 1, PageSize = 20
        }, CancellationToken.None);
        var breached = await sut.GetBreachedTicketsAsync(new SlaTicketListQuery
        {
            TenantId = "tenant-a", Page = 1, PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(["sqlite-completed-new", "sqlite-completed-old"], completed.Items.Select(x => x.TicketId));
        Assert.Equal(["sqlite-breached-early", "sqlite-breached-late"], breached.Items.Select(x => x.TicketId));
    }

    private static TicketSlaState CreateBreachedState(string ticketId, DateTimeOffset dueAt)
    {
        return new TicketSlaState
        {
            TicketId = ticketId,
            StartedAt = dueAt.AddHours(-2),
            ResponseDueAt = dueAt.AddHours(-1),
            ResolutionDueAt = dueAt,
            Status = SlaStatus.Breached,
            ResponseBreached = true,
            ResolutionBreached = true
        };
    }

    private static SlaReportingQueryService CreateSut(HelpdeskDbContext db)
    {
        return new SlaReportingQueryService(
            db,
            new SlaClockService(),
            Options.Create(new SlaReportingOptions { NearBreachCandidateLimit = 500 }));
    }

    private static HelpdeskDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HelpdeskDbContext(options, new TestTenantContext(), new HttpContextAccessor());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;
    }
}
