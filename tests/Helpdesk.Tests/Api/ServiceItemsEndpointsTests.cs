using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Services;
using Helpdesk.API.Services;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Service;
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
using Xunit;

namespace Helpdesk.Tests.Api;

public sealed class ServiceItemsEndpointsTests
{
    [Fact]
    public async Task GetServiceItems_ReturnsRecursiveVisibleRequestCounts_ForServiceFolders()
    {
        await using var harness = await ServiceItemsTestHarness.CreateAsync();

        var rootResponse = await harness.Client.GetFromJsonAsync<List<ServiceItemDto>>("/api/v1/service-items");
        var nestedResponse = await harness.Client.GetFromJsonAsync<List<ServiceItemDto>>("/api/v1/service-items/root-a");

        Assert.NotNull(rootResponse);
        var rootService = Assert.Single(rootResponse!, item => item.Id == "root-a");
        Assert.Equal(ServiceItemType.Service, rootService.ItemType);
        Assert.Equal(3, rootService.AvailableRequestCount);
        Assert.DoesNotContain(rootResponse!, item => item.Id == "root-b");

        Assert.NotNull(nestedResponse);
        var childService = Assert.Single(nestedResponse!, item => item.Id == "child-a");
        Assert.Equal(2, childService.AvailableRequestCount);
        Assert.Contains(nestedResponse!, item => item.Id == "form-root" && item.ItemType == ServiceItemType.RequestForm);
        Assert.DoesNotContain(nestedResponse!, item => item.Id == "form-testing");
    }

    [Fact]
    public async Task SearchServiceItems_ReturnsServicesAndRequestForms()
    {
        await using var harness = await ServiceItemsTestHarness.CreateAsync(isHelpdeskAdmin: true);

        var response = await harness.Client.GetFromJsonAsync<PagedResponse<ServiceItemDto>>(
            "/api/v1/service-items/search?pageSize=10");

        Assert.NotNull(response);
        Assert.Equal(1, response!.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Contains(response.Items, item => item.Id == "root-a" && item.ItemType == ServiceItemType.Service);
        Assert.Contains(response.Items, item => item.Id == "form-root" && item.ItemType == ServiceItemType.RequestForm);
        Assert.Contains(response.Items, item => item.Id == "root-b" && item.ItemType == ServiceItemType.Service);
        Assert.Contains(response.Items, item => item.Id == "form-testing" && item.ItemType == ServiceItemType.RequestForm);
    }

    [Fact]
    public async Task SearchServiceItems_FiltersServiceAllowedOrganizationsInMemory_ForTenantUsers()
    {
        await using var harness = await ServiceItemsTestHarness.CreateAsync();

        var httpResponse = await harness.Client.GetAsync(
            "/api/v1/service-items/search?pageSize=10");
        var body = await httpResponse.Content.ReadAsStringAsync();
        Assert.True(httpResponse.IsSuccessStatusCode, body);
        var response = await httpResponse.Content.ReadFromJsonAsync<PagedResponse<ServiceItemDto>>();

        Assert.NotNull(response);
        Assert.Contains(response!.Items, item => item.Id == "root-a" && item.ItemType == ServiceItemType.Service);
        Assert.Contains(response.Items, item => item.Id == "form-root" && item.ItemType == ServiceItemType.RequestForm);
        Assert.DoesNotContain(response.Items, item => item.Id == "root-b");
        Assert.DoesNotContain(response.Items, item => item.Id == "form-testing");
    }

    [Fact]
    public async Task GetServiceBreadcrumb_DoesNotRevealAnInaccessibleParent()
    {
        await using var harness = await ServiceItemsTestHarness.CreateAsync();

        var response = await harness.Client.GetFromJsonAsync<List<BreadcrumbDto>>(
            "/api/v1/services/tenant-child/breadcrumb");

        var breadcrumb = Assert.Single(response!);
        Assert.Equal("tenant-child", breadcrumb.Id);
        Assert.Equal("Tenant child", breadcrumb.Name);
    }

    private sealed class ServiceItemsTestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private ServiceItemsTestHarness(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            _connection = connection;
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<ServiceItemsTestHarness> CreateAsync(bool isHelpdeskAdmin = false)
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
            builder.Services.AddScoped<IRepository<Service>, EfRepository<Service>>();
            builder.Services.AddScoped<IRepository<RequestForm>, EfRepository<RequestForm>>();
            builder.Services.AddScoped<ITenantContext>(_ => new TestTenantContext("tenant-1", "user-1", isHelpdeskAdmin));
            builder.Services.AddScoped<ISelfServiceAudienceService, TestSelfServiceAudienceService>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapServiceEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.EnsureCreatedAsync();

                db.Services.AddRange(
                    new Service { Id = "root-a", Name = "Root A", Description = "Visible root" },
                    new Service { Id = "child-a", Name = "Child A", Description = "Visible child", ParentServiceId = "root-a" },
                    new Service { Id = "root-b", Name = "Root B", Description = "Hidden root", AllowedOrganizationIds = ["tenant-2"] },
                    new Service { Id = "private-parent", Name = "Private parent", Description = "Hidden parent", AllowedOrganizationIds = ["tenant-2"] },
                    new Service { Id = "tenant-child", Name = "Tenant child", Description = "Visible child", ParentServiceId = "private-parent", AllowedOrganizationIds = ["tenant-1"] });

                db.RequestForms.AddRange(
                    new RequestForm { Id = "form-root", Title = "Root request", Description = "Direct request", ServiceId = "root-a", OrganizationId = "tenant-1", ReleaseStatus = RequestFormReleaseStatus.Production },
                    new RequestForm { Id = "form-child-1", Title = "Child request 1", Description = "Child request", ServiceId = "child-a", OrganizationId = "tenant-1", ReleaseStatus = RequestFormReleaseStatus.Production },
                    new RequestForm { Id = "form-child-2", Title = "Child request 2", Description = "Child request", ServiceId = "child-a", OrganizationId = "tenant-1", ReleaseStatus = RequestFormReleaseStatus.Production },
                    new RequestForm { Id = "form-hidden", Title = "Hidden request", Description = "Hidden", ServiceId = "root-b", OrganizationId = "tenant-1", ReleaseStatus = RequestFormReleaseStatus.Production },
                    new RequestForm { Id = "form-testing", Title = "Testing request", Description = "Testing", ServiceId = "root-a", OrganizationId = "tenant-1", ReleaseStatus = RequestFormReleaseStatus.InTesting });

                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "Requester");

            return new ServiceItemsTestHarness(connection, app, client);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestSelfServiceAudienceService : ISelfServiceAudienceService
    {
        public Task<bool> IsTestUserAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> CanAccessRequestFormAsync(RequestForm requestForm, CancellationToken cancellationToken) =>
            Task.FromResult(requestForm.ReleaseStatus == RequestFormReleaseStatus.Production);

        public IQueryable<RequestForm> ApplyAudienceFilter(
            IQueryable<RequestForm> query,
            bool isAdmin,
            bool isTestUser,
            string? tenantId)
        {
            if (isAdmin)
            {
                return query;
            }

            return query.Where(f =>
                (string.IsNullOrWhiteSpace(f.OrganizationId) || f.OrganizationId == tenantId)
                && f.ReleaseStatus == RequestFormReleaseStatus.Production);
        }
    }

    private sealed class TestTenantContext(string? tenantId, string? userId, bool isHelpdeskAdmin) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
        public string? UserId { get; } = userId;
        public bool IsHelpdeskAdmin { get; } = isHelpdeskAdmin;
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
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "Requester")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
