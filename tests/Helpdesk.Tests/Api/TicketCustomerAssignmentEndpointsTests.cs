using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Tickets;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketRequest = Helpdesk.Shared.Models.Request;

namespace Helpdesk.Tests.Api;

public sealed class TicketCustomerAssignmentEndpointsTests
{
    [Theory]
    [InlineData("incidents", "incident-1")]
    [InlineData("requests", "request-1")]
    [InlineData("changes", "change-1")]
    public async Task CustomerAssignment_Saves_ValidScopedCustomer(string ticketType, string ticketId)
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync();
        await harness.SeedTicketAsync(ticketType, ticketId, "org-1");
        await harness.SeedCustomerAsync("customer-1", "org-1", "Example User", "user@example.com");

        var response = await harness.Client.PostAsJsonAsync(
            $"/api/v1/{ticketType}/{ticketId}/customer",
            new { CustomerId = "customer-1" });

        response.EnsureSuccessStatusCode();
        var ticket = await harness.GetTicketAsync(ticketType, ticketId);
        Assert.Equal("customer-1", ticket!.CustomerId);
        Assert.Equal("user@example.com", ticket.RequesterEmail);
        Assert.NotNull(ticket.UpdatedAt);
    }

    [Fact]
    public async Task CustomerAssignment_AllowsTechnician()
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync(role: "Technician");
        await harness.SeedTicketAsync("incidents", "incident-1", "org-1");
        await harness.SeedCustomerAsync("customer-1", "org-1", "Technician User", "tech-user@example.com");

        var response = await harness.Client.PostAsJsonAsync(
            "/api/v1/incidents/incident-1/customer",
            new { CustomerId = "customer-1" });

        response.EnsureSuccessStatusCode();
        var ticket = await harness.GetTicketAsync("incidents", "incident-1");
        Assert.Equal("customer-1", ticket!.CustomerId);
    }

    [Fact]
    public async Task CustomerAssignment_RejectsTechnicianOutsideTheirTenantScope()
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync(role: "Technician");
        await harness.SeedTicketAsync("incidents", "incident-1", "org-2");
        await harness.SeedCustomerAsync("customer-1", "org-2", "Other Tenant User", "other-tenant@example.com");

        var response = await harness.Client.PostAsJsonAsync(
            "/api/v1/incidents/incident-1/customer",
            new { CustomerId = "customer-1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var ticket = await harness.GetTicketAsync("incidents", "incident-1");
        Assert.Null(ticket!.CustomerId);
    }

    [Fact]
    public async Task CustomerAssignment_RejectsCustomerFromDifferentOrganization()
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync();
        await harness.SeedTicketAsync("incidents", "incident-1", "org-1");
        await harness.SeedCustomerAsync("customer-2", "org-2", "Other Customer", "other@example.com");

        var response = await harness.Client.PostAsJsonAsync(
            "/api/v1/incidents/incident-1/customer",
            new { CustomerId = "customer-2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var ticket = await harness.GetTicketAsync("incidents", "incident-1");
        Assert.Null(ticket!.CustomerId);
    }

    [Fact]
    public async Task CustomerAssignment_RejectsDisabledCustomer()
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync();
        await harness.SeedTicketAsync("requests", "request-1", "org-1");
        await harness.SeedCustomerAsync("customer-1", "org-1", "Blocked Customer", "blocked@example.com", enabled: false);

        var response = await harness.Client.PostAsJsonAsync(
            "/api/v1/requests/request-1/customer",
            new { CustomerId = "customer-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var ticket = await harness.GetTicketAsync("requests", "request-1");
        Assert.Null(ticket!.CustomerId);
    }

    [Fact]
    public async Task CustomerAssignment_RejectsTicketThatAlreadyHasCustomer()
    {
        await using var harness = await TicketCustomerAssignmentHarness.CreateAsync();
        await harness.SeedTicketAsync("changes", "change-1", "org-1", customerId: "existing-customer");
        await harness.SeedCustomerAsync("customer-1", "org-1", "New Customer", "new@example.com");

        var response = await harness.Client.PostAsJsonAsync(
            "/api/v1/changes/change-1/customer",
            new { CustomerId = "customer-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var ticket = await harness.GetTicketAsync("changes", "change-1");
        Assert.Equal("existing-customer", ticket!.CustomerId);
    }

    private sealed class TicketCustomerAssignmentHarness : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly InMemoryRepository<Incident> incidents;
        private readonly InMemoryRepository<TicketRequest> requests;
        private readonly InMemoryRepository<Change> changes;
        private readonly InMemoryRepository<Customer> customers;

        private TicketCustomerAssignmentHarness(
            WebApplication app,
            HttpClient client,
            InMemoryRepository<Incident> incidents,
            InMemoryRepository<TicketRequest> requests,
            InMemoryRepository<Change> changes,
            InMemoryRepository<Customer> customers)
        {
            this.app = app;
            this.incidents = incidents;
            this.requests = requests;
            this.changes = changes;
            this.customers = customers;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TicketCustomerAssignmentHarness> CreateAsync(string role = "HelpdeskAdmin")
        {
            var incidents = new InMemoryRepository<Incident>();
            var requests = new InMemoryRepository<TicketRequest>();
            var changes = new InMemoryRepository<Change>();
            var customers = new InMemoryRepository<Customer>();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IRepository<Incident>>(incidents);
            builder.Services.AddSingleton<IRepository<TicketRequest>>(requests);
            builder.Services.AddSingleton<IRepository<Change>>(changes);
            builder.Services.AddSingleton<IRepository<Customer>>(customers);
            builder.Services.AddSingleton<ICurrentUserAccessService, TestUserAccessService>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();
            var ticketGroup = app.MapGroup("/api/v1/{ticketType}/{ticketId}")
                .RequireAuthorization();
            TicketEndpoints.MapTicketCustomerEndpoint(ticketGroup);

            await app.StartAsync();
            var harness = new TicketCustomerAssignmentHarness(app, app.GetTestClient(), incidents, requests, changes, customers);
            harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", role);
            return harness;
        }

        public async Task SeedTicketAsync(string ticketType, string ticketId, string organizationId, string? customerId = null)
        {
            switch (ticketType)
            {
                case "incidents":
                    await incidents.CreateAsync(new Incident
                    {
                        Id = ticketId,
                        Title = "Incident",
                        TrackingId = "INC-TEST",
                        OrganizationId = organizationId,
                        CustomerId = customerId
                    });
                    break;
                case "requests":
                    await requests.CreateAsync(new TicketRequest
                    {
                        Id = ticketId,
                        Title = "Request",
                        TrackingId = "REQ-TEST",
                        OrganizationId = organizationId,
                        CustomerId = customerId
                    });
                    break;
                case "changes":
                    await changes.CreateAsync(new Change
                    {
                        Id = ticketId,
                        Title = "Change",
                        TrackingId = "CHG-TEST",
                        OrganizationId = organizationId,
                        CustomerId = customerId
                    });
                    break;
            }
        }

        public async Task SeedCustomerAsync(string id, string organizationId, string name, string email, bool enabled = true)
        {
            await customers.CreateAsync(new Customer
            {
                Id = id,
                OrganizationId = organizationId,
                Name = name,
                Email = email,
                IsEnabled = enabled
            });
        }

        public async Task<Ticket?> GetTicketAsync(string ticketType, string ticketId) =>
            ticketType switch
            {
                "incidents" => await incidents.GetAsync(ticketId),
                "requests" => await requests.GetAsync(ticketId),
                "changes" => await changes.GetAsync(ticketId),
                _ => null
            };

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers.Authorization.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test"),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class TestUserAccessService : ICurrentUserAccessService
    {
        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (user.IsInRole(HelpdeskPermissions.HelpdeskAdmin))
            {
                return Task.FromResult(new CurrentUserAccessProfile(
                    true,
                    "Test",
                    null,
                    null,
                    null,
                    null,
                    true,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            }

            var permissions = user.IsInRole("Technician")
                ? HelpdeskPermissions.TechnicalBundle.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new CurrentUserAccessProfile(
                true,
                "Test",
                null,
                "org-1",
                null,
                null,
                false,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { HelpdeskRoleBundles.Technical },
                permissions,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "org-1" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }
    }
}
