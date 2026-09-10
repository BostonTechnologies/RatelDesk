using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Notifications;
using Helpdesk.API.Endpoints.Ticketing;
using Helpdesk.Infrastructure.Auth.Rbac;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Persistence.Entities;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Customer;
using Helpdesk.Shared.DTOs.Notification;
using Helpdesk.Shared.DTOs.Organization;
using Helpdesk.Shared.DTOs.User;
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

public sealed class TenantRbacHardeningEndpointsTests
{
    [Fact]
    public async Task CustomerTicketingLookups_ReturnOnlyPrimaryTenantAndOwnCustomer()
    {
        await using var harness = await TenantRbacHardeningHarness.CreateAsync("Customer");

        var organizations = await harness.Client.GetFromJsonAsync<List<OrganizationDto>>("/api/v1/ticketing/organizations");
        var customers = await harness.Client.GetFromJsonAsync<List<CustomerDto>>("/api/v1/ticketing/customers?organizationId=org-alpha");
        var assignees = await harness.Client.GetFromJsonAsync<List<UserDto>>("/api/v1/ticketing/assignees?organizationId=org-alpha");
        var crossTenant = await harness.Client.GetAsync("/api/v1/ticketing/customers?organizationId=org-other");

        var organization = Assert.Single(organizations!);
        Assert.Equal("org-alpha", organization.Id);
        var customer = Assert.Single(customers!);
        Assert.Equal("customer-primary", customer.Id);
        Assert.Empty(assignees!);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
    }

    [Fact]
    public async Task TechnicalTicketingLookups_ReturnPrimaryAndMspManagedTenants()
    {
        await using var harness = await TenantRbacHardeningHarness.CreateAsync("Technical");

        var organizations = await harness.Client.GetFromJsonAsync<List<OrganizationDto>>("/api/v1/ticketing/organizations");
        var alphaCustomers = await harness.Client.GetFromJsonAsync<List<CustomerDto>>("/api/v1/ticketing/customers?organizationId=org-alpha");
        var assignees = await harness.Client.GetFromJsonAsync<List<UserDto>>("/api/v1/ticketing/assignees?organizationId=org-alpha");
        var unrelated = await harness.Client.GetAsync("/api/v1/ticketing/customers?organizationId=org-other");

        Assert.Equal(["org-alpha", "org-support"], organizations!.Select(x => x.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["customer-alpha-other", "customer-primary"], alphaCustomers!.Select(x => x.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(assignees!, x => x.Id == "user-alpha-tech");
        Assert.Equal(HttpStatusCode.Forbidden, unrelated.StatusCode);
    }

    [Fact]
    public async Task HelpdeskAdminTicketingLookups_ReturnAllEnabledTenantsAndCustomers()
    {
        await using var harness = await TenantRbacHardeningHarness.CreateAsync("Admin");

        var organizations = await harness.Client.GetFromJsonAsync<List<OrganizationDto>>("/api/v1/ticketing/organizations");
        var otherCustomers = await harness.Client.GetFromJsonAsync<List<CustomerDto>>("/api/v1/ticketing/customers?organizationId=org-other");

        Assert.Equal(["org-alpha", "org-other", "org-support"], organizations!.Select(x => x.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(otherCustomers!, x => x.Id == "customer-other");
    }

    [Fact]
    public async Task Notifications_AreScopedToAllowedTenantForTechnicalUsers()
    {
        await using var harness = await TenantRbacHardeningHarness.CreateAsync("Technical");

        var page = await harness.Client.GetFromJsonAsync<PagedResponse<NotificationDto>>("/api/v1/notifications?pageSize=50");
        var summary = await harness.Client.GetFromJsonAsync<NotificationSummaryDto>("/api/v1/notifications/summary");

        Assert.NotNull(page);
        Assert.Equal(["notification-alpha", "notification-own"], page!.Items.Select(x => x.Title).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, summary!.TotalCount);
        Assert.Equal(2, summary.UnreadCount);
    }

    [Fact]
    public async Task Notifications_GlobalSystemRowsRemainAdminOnly()
    {
        await using var technicalHarness = await TenantRbacHardeningHarness.CreateAsync("Technical");
        await using var adminHarness = await TenantRbacHardeningHarness.CreateAsync("Admin");

        var technicalPage = await technicalHarness.Client.GetFromJsonAsync<PagedResponse<NotificationDto>>("/api/v1/notifications?pageSize=50");
        var adminPage = await adminHarness.Client.GetFromJsonAsync<PagedResponse<NotificationDto>>("/api/v1/notifications?pageSize=50");

        Assert.DoesNotContain(technicalPage!.Items, x => x.Title == "notification-global");
        Assert.Contains(adminPage!.Items, x => x.Title == "notification-global");
    }

    private sealed class TenantRbacHardeningHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private TenantRbacHardeningHarness(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            _connection = connection;
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TenantRbacHardeningHarness> CreateAsync(string actor)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext("org-alpha", "customer-primary", actor == "Admin"));
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
                options.AddPolicy("NotificationAccess", policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(HelpdeskPermissions.ChangeManager, HelpdeskPermissions.HelpdeskAdmin);
                });
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapTicketingLookupEndpoints();
            app.MapNotificationEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.EnsureCreatedAsync();
                await SeedAsync(db);
            }

            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", actor);
            return new TenantRbacHardeningHarness(connection, app, client);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static async Task SeedAsync(HelpdeskDbContext db)
        {
            db.Organizations.AddRange(
                new Organization { Id = "org-alpha", Name = "Alpha Organization", ItSupportOrganizationId = "org-support" },
                new Organization { Id = "org-support", Name = "Support Organization" },
                new Organization { Id = "org-other", Name = "Other Organization" });

            db.Customers.AddRange(
                new Customer { Id = "customer-primary", Name = "Primary User", Email = "primary@example.com", OrganizationId = "org-alpha" },
                new Customer { Id = "customer-alpha-other", Name = "Other User", Email = "other@example.com", OrganizationId = "org-alpha" },
                new Customer { Id = "customer-support", Name = "Support User", Email = "support@example.com", OrganizationId = "org-support" },
                new Customer { Id = "customer-other", Name = "Other Admin", Email = "admin@example.com", OrganizationId = "org-other" });

            db.Users.AddRange(
                new User { Id = "user-alpha-tech", Name = "Alpha Technician", Email = "tech@example.com", OrganizationId = "org-alpha", Role = "Technician" },
                new User { Id = "user-support-tech", Name = "Support Technician", Email = "support-tech@example.com", OrganizationId = "org-support", Role = "Technician" },
                new User { Id = "user-other", Name = "Other Admin", Email = "admin@example.com", OrganizationId = "org-other", Role = "HelpdeskAdmin" });

            db.CustomerAuthLinks.AddRange(
                new CustomerAuthLink
                {
                    CustomerId = "customer-primary",
                    OidcIssuer = "https://auth.example/application/o/helpdesk-dev",
                    OidcSubject = "subject-customer",
                    AuthentikEmail = "primary@example.com",
                    InviteStatus = CustomerInviteStatus.Active
                },
                new CustomerAuthLink
                {
                    CustomerId = "customer-support",
                    OidcIssuer = "https://auth.example/application/o/helpdesk-dev",
                    OidcSubject = "subject-technical",
                    AuthentikEmail = "support@example.com",
                    InviteStatus = CustomerInviteStatus.Active
                });

            db.Notifications.AddRange(
                new NotificationEntity
                {
                    Id = Guid.NewGuid(),
                    Title = "notification-alpha",
                    Message = "Alpha exception",
                    Severity = NotificationSeverity.Critical,
                    CreatedUtc = DateTime.UtcNow,
                    TenantId = "org-alpha",
                    IsGlobal = true
                },
                new NotificationEntity
                {
                    Id = Guid.NewGuid(),
                    Title = "notification-other",
                    Message = "Other exception",
                    Severity = NotificationSeverity.Critical,
                    CreatedUtc = DateTime.UtcNow,
                    TenantId = "org-other",
                    IsGlobal = true
                },
                new NotificationEntity
                {
                    Id = Guid.NewGuid(),
                    Title = "notification-own",
                    Message = "Own notification",
                    Severity = NotificationSeverity.Info,
                    CreatedUtc = DateTime.UtcNow,
                    UserId = "subject-technical",
                    IsGlobal = false
                },
                new NotificationEntity
                {
                    Id = Guid.NewGuid(),
                    Title = "notification-global",
                    Message = "System-wide",
                    Severity = NotificationSeverity.Warning,
                    CreatedUtc = DateTime.UtcNow,
                    IsGlobal = true
                });

            await db.SaveChangesAsync();
        }
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
            if (!Request.Headers.TryGetValue("Authorization", out var header))
                return Task.FromResult(AuthenticateResult.Fail("No authorization header"));

            var actor = header.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Customer";
            Claim[] claims = actor switch
            {
                "Admin" =>
                [
                    new Claim(ClaimTypes.NameIdentifier, "subject-admin"),
                    new Claim(ClaimTypes.Email, "admin@example.com"),
                    new Claim(ClaimTypes.Role, HelpdeskPermissions.HelpdeskAdmin),
                    new Claim("roles", HelpdeskPermissions.HelpdeskAdmin)
                ],
                "Technical" =>
                [
                    new Claim(ClaimTypes.NameIdentifier, "subject-technical"),
                    new Claim("sub", "subject-technical"),
                    new Claim("iss", "https://auth.example/application/o/helpdesk-dev/"),
                    new Claim(ClaimTypes.Email, "support@example.com"),
                    new Claim("groups", AuthentikRbacGroups.Technical),
                    new Claim(ClaimTypes.Role, HelpdeskPermissions.ChangeManager)
                ],
                _ =>
                [
                    new Claim(ClaimTypes.NameIdentifier, "subject-customer"),
                    new Claim("sub", "subject-customer"),
                    new Claim("iss", "https://auth.example/application/o/helpdesk-dev/"),
                    new Claim(ClaimTypes.Email, "primary@example.com"),
                    new Claim("groups", AuthentikRbacGroups.LegacyCustomer)
                ]
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
