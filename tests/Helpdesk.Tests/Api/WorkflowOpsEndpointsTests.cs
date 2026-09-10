using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Ops;
using Helpdesk.Infrastructure.Auth.Rbac;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Request;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Api;

public sealed class WorkflowOpsEndpointsTests
{
    [Fact]
    public async Task RequestManager_CanReadTenantScopedWorkflowOps_ButNotRetriesPending()
    {
        await using var harness = await WorkflowOpsHarness.CreateAsync("RequestManager");

        var overdue = await harness.Client.GetAsync("/api/v1/ops/tasks/overdue?page=1&pageSize=25");
        var escalated = await harness.Client.GetAsync("/api/v1/ops/tasks/escalated?page=1&pageSize=25");
        var critical = await harness.Client.GetAsync(
            $"/api/v1/ops/tasks/critical-failures?sinceUtc={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&page=1&pageSize=25");
        var retries = await harness.Client.GetAsync("/api/v1/ops/tasks/retries-pending?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, overdue.StatusCode);
        Assert.Equal(HttpStatusCode.OK, escalated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, critical.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, retries.StatusCode);
    }

    [Fact]
    public async Task HelpdeskAdmin_CanReadRetriesPending()
    {
        await using var harness = await WorkflowOpsHarness.CreateAsync("Admin");

        var response = await harness.Client.GetAsync("/api/v1/ops/tasks/retries-pending?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class WorkflowOpsHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private WorkflowOpsHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<WorkflowOpsHarness> CreateAsync(string actor)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseInMemoryDatabase($"workflow-ops-{Guid.NewGuid():N}"));
            builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext("org-alpha", "user-1", actor == "Admin"));
            builder.Services.AddScoped<ICurrentUserAccessService, CurrentUserAccessService>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("HelpdeskAdmin", policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(HelpdeskPermissions.HelpdeskAdmin);
                });
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapWorkflowOpsEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.EnsureCreatedAsync();
                Seed(db);
                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", actor);
            return new WorkflowOpsHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }

        private static void Seed(HelpdeskDbContext db)
        {
            db.Requests.AddRange(
                new Request { Id = "req-alpha", TrackingId = "REQ-ALPHA", Title = "Alpha request", OrganizationId = "org-alpha" },
                new Request { Id = "req-other", TrackingId = "REQ-OTH", Title = "Other request", OrganizationId = "org-other" });

            db.RequestTasks.AddRange(
                NewTask("task-overdue", "req-alpha", "org-alpha", RequestTaskStatus.InProgress, dueAt: DateTimeOffset.UtcNow.AddHours(-2)),
                NewTask("task-escalated", "req-alpha", "org-alpha", RequestTaskStatus.InProgress, escalated: true),
                NewTask("task-critical", "req-alpha", "org-alpha", RequestTaskStatus.Failed, isCritical: true),
                NewTask("task-retry", "req-alpha", "org-alpha", RequestTaskStatus.Failed, nextRetryAt: DateTimeOffset.UtcNow.AddMinutes(30)),
                NewTask("task-other", "req-other", "org-other", RequestTaskStatus.InProgress, dueAt: DateTimeOffset.UtcNow.AddHours(-3)));
        }

        private static RequestTask NewTask(
            string id,
            string requestId,
            string organizationId,
            RequestTaskStatus status,
            DateTimeOffset? dueAt = null,
            DateTimeOffset? nextRetryAt = null,
            bool escalated = false,
            bool isCritical = false) => new()
            {
                Id = id,
                TrackingId = id,
                Title = id,
                RequestId = requestId,
                OrganizationId = organizationId,
                Status = status,
                Type = RequestTaskType.Manual,
                DueAt = dueAt,
                NextRetryAt = nextRetryAt,
                Escalated = escalated,
                IsCritical = isCritical,
                UpdatedAt = isCritical ? DateTime.UtcNow : null
            };
    }

    private sealed class TestTenantContext(string? tenantId, string? userId, bool isHelpdeskAdmin) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
        public string? UserId { get; } = userId;
        public bool IsHelpdeskAdmin { get; } = isHelpdeskAdmin;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers.Authorization.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "RequestManager";
            var claims = actor == "Admin"
                ? new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                    new Claim(ClaimTypes.Role, HelpdeskPermissions.HelpdeskAdmin)
                }
                : new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "manager-1"),
                    new Claim("organization_id", "org-alpha"),
                    new Claim(ClaimTypes.Role, HelpdeskPermissions.RequestManager),
                    new Claim("roles", HelpdeskPermissions.RequestManager)
                };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
