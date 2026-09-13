using System.Net;
using System.Net.Http.Json;
using Helpdesk.API.Endpoints.Tickets;
using Helpdesk.Application.Tickets;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Worklog;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Api;

public sealed class PublicTicketEndpointsTests
{
    [Fact]
    public async Task View_Returns_Incident_For_Valid_Signed_Tracking_Link()
    {
        await using var harness = await PublicTicketTestHarness.CreateAsync();
        await harness.SeedTicketAsync(new Incident
        {
            Id = "incident-1",
            TrackingId = "INC-PUB-001",
            Title = "Router down",
            OrganizationId = "org-1",
            RequesterEmail = "customer@example.com"
        });

        var response = await harness.Client.GetAsync(
            "/api/v1/tickets/public/view?trackingId=INC-PUB-001&email=customer%40example.com&token=token:INC-PUB-001:customer%40example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PublicTicketViewDto>();
        Assert.Equal("INC-PUB-001", dto!.TrackingId);
        Assert.Equal("Router down", dto.Title);
    }

    [Fact]
    public async Task View_Returns_Request_For_Valid_Signed_Tracking_Link()
    {
        await using var harness = await PublicTicketTestHarness.CreateAsync();
        await harness.SeedTicketAsync(new Request
        {
            Id = "request-1",
            TrackingId = "REQ-PUB-001",
            Title = "New laptop",
            OrganizationId = "org-1",
            RequesterEmail = "customer@example.com"
        });

        var response = await harness.Client.GetAsync(
            "/api/v1/tickets/public/view?trackingId=REQ-PUB-001&email=customer%40example.com&token=token:REQ-PUB-001:customer%40example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PublicTicketViewDto>();
        Assert.Equal("REQ-PUB-001", dto!.TrackingId);
        Assert.Equal("New laptop", dto.Title);
    }

    [Fact]
    public async Task View_Rejects_Invalid_Token()
    {
        await using var harness = await PublicTicketTestHarness.CreateAsync();
        await harness.SeedTicketAsync(new Incident
        {
            Id = "incident-1",
            TrackingId = "INC-PUB-001",
            Title = "Router down",
            OrganizationId = "org-1"
        });

        var response = await harness.Client.GetAsync(
            "/api/v1/tickets/public/view?trackingId=INC-PUB-001&email=customer%40example.com&token=wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task View_Rejects_Missing_Token()
    {
        await using var harness = await PublicTicketTestHarness.CreateAsync();

        var response = await harness.Client.GetAsync(
            "/api/v1/tickets/public/view?trackingId=INC-PUB-001&email=customer%40example.com&token=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Timeline_Returns_Only_Public_Replies_In_Instant_Order_On_Migrated_Sqlite()
    {
        await using var harness = await PublicTicketTestHarness.CreateAsync();
        await harness.SeedTicketAsync(new Incident { Id = "timeline-ticket", TrackingId = "INC-TIMELINE" });
        await harness.SeedTicketAsync(new Incident { Id = "other-ticket", TrackingId = "INC-OTHER" });
        var first = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        await harness.SeedTimelineAsync(
            new() { TicketId = "timeline-ticket", EventType = TimelineEventType.CustomerReply, CreatedUtc = first, MessageText = "First" },
            new() { TicketId = "timeline-ticket", EventType = TimelineEventType.TechnicianReply, CreatedUtc = first.AddMinutes(1).ToOffset(TimeSpan.FromHours(-4)), MessageText = "Second" },
            new() { TicketId = "timeline-ticket", EventType = TimelineEventType.SystemNotification, CreatedUtc = first.AddMinutes(2), MessageText = "Private system event" },
            new() { TicketId = "other-ticket", EventType = TimelineEventType.CustomerReply, CreatedUtc = first.AddMinutes(3), MessageText = "Other ticket" });

        var response = await harness.Client.GetAsync(
            "/api/v1/tickets/public/timeline?trackingId=INC-TIMELINE&email=customer%40example.com&token=token:INC-TIMELINE:customer%40example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<TicketTimelineEventDto>>();
        Assert.Equal(new[] { "Second", "First" }, items!.Select(x => x.MessageText));
    }

    private sealed class PublicTicketTestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly WebApplication app;

        private PublicTicketTestHarness(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            this.connection = connection;
            this.app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<PublicTicketTestHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(connection, sqlite => sqlite.MigrationsAssembly("Helpdesk.Infrastructure.SqliteMigrations")));
            builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext("org-1", "admin-1", isHelpdeskAdmin: true));
            builder.Services.AddSingleton<IPublicTicketLinkSigner, TestPublicTicketLinkSigner>();

            var app = builder.Build();
            app.MapPublicTicketEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.MigrateAsync();
            }

            await app.StartAsync();
            return new PublicTicketTestHarness(connection, app, app.GetTestClient());
        }

        public async Task SeedTicketAsync(Ticket ticket)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
        }

        public async Task SeedTimelineAsync(params TicketTimelineEvent[] events)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            db.TicketTimelineEvents.AddRange(events);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestPublicTicketLinkSigner : IPublicTicketLinkSigner
    {
        public string GenerateToken(string trackingId, string email, DateTimeOffset expires) => $"token:{trackingId}:{email}";

        public bool ValidateToken(string token, string trackingId, string email) =>
            token == GenerateToken(trackingId, email, DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class TestTenantContext(string? tenantId, string? userId, bool isHelpdeskAdmin) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
        public string? UserId { get; } = userId;
        public bool IsHelpdeskAdmin { get; } = isHelpdeskAdmin;
    }
}
