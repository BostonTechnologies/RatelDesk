using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Requests;
using Helpdesk.API.Services;
using Helpdesk.Application.Events;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Helpdesk.Tests.Api;

[Collection("SelfServiceMyRequestsEndpoints")]
public sealed class SelfServiceMyRequestsEndpointsTests
{
    [Fact]
    public async Task GetMyRequests_ReturnsAccessible_Request_WithReleaseStatus()
    {
        await using var harness = await SelfServiceMyRequestsTestHarness.CreateAsync();

        var response = await harness.Client.GetAsync("/api/v1/self-service/requests");
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<MyRequestListItemDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal(harness.RequestId, payload.Items[0].Id);
        Assert.Equal(RequestFormReleaseStatus.Production, payload.Items[0].ReleaseStatus);
    }

    [Fact]
    public async Task GetMyRequestDetail_ReturnsOwned_Request()
    {
        await using var harness = await SelfServiceMyRequestsTestHarness.CreateAsync();

        var response = await harness.Client.GetAsync($"/api/v1/self-service/requests/{harness.RequestId}");
        var payload = await response.Content.ReadFromJsonAsync<MyRequestDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(harness.RequestId, payload!.Id);
        Assert.Equal(RequestFormReleaseStatus.Production, payload.ReleaseStatus);
        Assert.Equal("Need laptop access", payload.Title);
    }

    [Fact]
    public async Task GetMyRequests_DoesNotUseMatchingEmailWithoutAnExplicitCustomerLink()
    {
        await using var harness = await SelfServiceMyRequestsTestHarness.CreateAsync("EmailOnly");

        var response = await harness.Client.GetAsync("/api/v1/self-service/requests");
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<MyRequestListItemDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.TotalCount);
        Assert.Empty(payload.Items);
    }

    private sealed class SelfServiceMyRequestsTestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private SelfServiceMyRequestsTestHarness(
            SqliteConnection connection,
            WebApplication app,
            HttpClient client,
            string requestId)
        {
            _connection = connection;
            _app = app;
            Client = client;
            RequestId = requestId;
        }

        public HttpClient Client { get; }
        public string RequestId { get; }

        public static async Task<SelfServiceMyRequestsTestHarness> CreateAsync(string authHeader = "SelfService")
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
            builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext("tenant-1", "user-1", isHelpdeskAdmin: false));
            builder.Services.AddScoped<ISelfServiceAudienceService, AllowingSelfServiceAudienceService>();
            builder.Services.AddScoped<ICurrentUserAccessService, ClaimsCurrentUserAccessService>();
            builder.Services.AddSingleton<IDomainEventPublisher, TestDomainEventPublisher>();
            builder.Services.AddScoped<ICorrelationContext>(_ => new TestCorrelationContext("corr-self-service-tests"));
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("SelfService.User", policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("SelfService.User", "HelpdeskAdmin");
                });
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapSelfServiceMyRequestsEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.EnsureCreatedAsync();

                var user = new User
                {
                    Id = "user-1",
                    Name = "Requester",
                    Email = "requester@example.com",
                    OrganizationId = "tenant-1",
                    Role = "Customer",
                    IsTestUser = false
                };

                var customer = new Customer
                {
                    Id = "customer-1",
                    Name = "Requester",
                    Email = "requester@example.com",
                    OrganizationId = "tenant-1"
                };

                var form = new RequestForm
                {
                    Id = "form-1",
                    Title = "Laptop Access",
                    Description = "Request access",
                    ServiceId = "service-1",
                    OrganizationId = "tenant-1",
                    ReleaseStatus = RequestFormReleaseStatus.Production
                };

                var request = new Request
                {
                    Id = "request-1",
                    TrackingId = "REQ-001",
                    Title = "Need laptop access",
                    Description = "Please grant laptop access",
                    CustomerId = "customer-1",
                    OrganizationId = "tenant-1",
                    RequestFormId = "form-1",
                    State = TicketState.New,
                    CreatedAt = DateTime.UtcNow
                };

                var otherRequest = new Request
                {
                    Id = "request-2",
                    TrackingId = "REQ-002",
                    Title = "Other request",
                    Description = "Should stay hidden",
                    CustomerId = "customer-2",
                    OrganizationId = "tenant-1",
                    RequestFormId = "form-1",
                    State = TicketState.New,
                    CreatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                db.Customers.Add(customer);
                db.RequestForms.Add(form);
                db.Requests.Add(request);
                db.Requests.Add(otherRequest);
                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", authHeader);
            return new SelfServiceMyRequestsTestHarness(connection, app, client, "request-1");
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestTenantContext(string? tenantId, string? userId, bool isHelpdeskAdmin) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
        public string? UserId { get; } = userId;
        public bool IsHelpdeskAdmin { get; } = isHelpdeskAdmin;
    }

    private sealed class TestDomainEventPublisher : IDomainEventPublisher
    {
        public Task PublishAsync(DomainEvent domainEvent, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AllowingSelfServiceAudienceService : ISelfServiceAudienceService
    {
        public Task<bool> IsTestUserAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> CanAccessRequestFormAsync(RequestForm requestForm, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public IQueryable<RequestForm> ApplyAudienceFilter(
            IQueryable<RequestForm> query,
            bool isAdmin,
            bool isTestUser,
            string? tenantId) =>
            query.Where(f =>
                f.OrganizationId == tenantId &&
                f.ReleaseStatus == RequestFormReleaseStatus.Production);
    }

    private sealed class TestCorrelationContext(string correlationId) : ICorrelationContext
    {
        public string GetCorrelationId() => correlationId;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = Request.Headers.Authorization.ToString().Contains("EmailOnly", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(ClaimTypes.Name, "Requester"),
                    new Claim("preferred_username", "requester@example.com"),
                    new Claim(ClaimTypes.Role, "SelfService.User")
                }
                : new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "Requester"),
                new Claim("preferred_username", "requester@example.com"),
                new Claim("customer_id", "customer-1"),
                new Claim(ClaimTypes.Role, "SelfService.User")
            };

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class ClaimsCurrentUserAccessService : ICurrentUserAccessService
    {
        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
            Task.FromResult(CurrentUserAccessProfile.FromClaims(user));
    }
}

[CollectionDefinition("SelfServiceMyRequestsEndpoints", DisableParallelization = true)]
public sealed class SelfServiceMyRequestsEndpointsCollection
{
}
