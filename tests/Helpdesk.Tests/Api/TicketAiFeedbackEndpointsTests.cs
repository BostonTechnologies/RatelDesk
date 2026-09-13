using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Tickets;
using Helpdesk.Application.Messaging;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Api;

public sealed class TicketAiFeedbackEndpointsTests
{
    [Fact]
    public async Task AutomationFeedback_RejectsRequestFromAnotherOrganization()
    {
        await using var harness = await TicketAiFeedbackHarness.CreateAsync();
        var ticketId = Guid.NewGuid().ToString();
        await harness.SeedAsync(db =>
        {
            db.Incidents.Add(new Incident
            {
                Id = ticketId,
                TrackingId = "INC-ALPHA",
                Title = "Tenant alpha incident",
                OrganizationId = "org-alpha"
            });
            db.Requests.Add(new Request
            {
                Id = "request-beta",
                TrackingId = "REQ-BETA",
                Title = "Tenant beta request",
                OrganizationId = "org-beta"
            });
        });

        var response = await harness.Client.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/ai-feedback",
            new SubmitTicketAiFeedbackDto
            {
                FeedbackType = "automation",
                FeedbackValue = "accepted",
                RequestId = "request-beta"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await harness.AssertNoFeedbackAsync();
    }

    private sealed class TicketAiFeedbackHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly WebApplication app;

        private TicketAiFeedbackHarness(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            this.connection = connection;
            this.app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TicketAiFeedbackHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddSingleton<ITenantContext>(new TestTenantContext());
            builder.Services.AddSingleton<ICurrentUserAccessService, TenantAlphaIncidentManagerAccessService>();
            builder.Services.AddSingleton<IRequestSender, FailingRequestSender>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            var ticketGroup = app.MapGroup("/api/v1/tickets/{id:guid}")
                .RequireAuthorization();
            TicketEndpoints.MapTicketAiFeedbackEndpoint(ticketGroup);

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "incident-manager");
            return new TicketAiFeedbackHarness(connection, app, client);
        }

        public async Task SeedAsync(Action<HelpdeskDbContext> seed)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }

        public async Task AssertNoFeedbackAsync()
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            Assert.Empty(await db.TicketAiFeedback.ToListAsync());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TenantAlphaIncidentManagerAccessService : ICurrentUserAccessService
    {
        private static readonly CurrentUserAccessProfile Profile = new(
            IsAuthenticated: true,
            Name: "Tenant alpha manager",
            Email: "manager@example.test",
            PrimaryOrganizationId: "org-alpha",
            PrimaryOrganizationName: "Tenant alpha",
            CustomerId: null,
            IsHelpdeskAdmin: false,
            RoleBundles: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Permissions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { HelpdeskPermissions.IncidentManager },
            AllowedOrganizationIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "org-alpha" },
            ManagedOrganizationIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
            Task.FromResult(Profile);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public string? TenantId => "org-alpha";
        public string? UserId => "manager-alpha";
        public bool IsHelpdeskAdmin => false;
    }

    private sealed class FailingRequestSender : IRequestSender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Unexpected request dispatch: {request.GetType().Name}");
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "manager-alpha")],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
