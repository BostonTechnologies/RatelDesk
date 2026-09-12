using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API;
using Helpdesk.Application.Sla;
using Helpdesk.Shared.DTOs;
using Helpdesk.Shared.DTOs.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Api;

public class SlaReportEndpointsAuthorizationTests
{
    [Fact]
    public async Task NonAdmin_QueryingAnUngrantedTenant_IsForbidden()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("RUN_MIGRATIONS", "false");

        var fake = new FakeReportingService();
        using var factory = CreateFactory(fake);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "Technician");

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync($"/api/v1/reports/sla/compliance?tenantId=tenant-b&fromUtc={from}&toUtc={to}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(fake.LastComplianceTenantId);
    }

    [Fact]
    public async Task ScopedManager_CanQueryAnExplicitlyGrantedTenant()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("RUN_MIGRATIONS", "false");

        var fake = new FakeReportingService();
        using var factory = CreateFactory(fake);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "ScopedTechnician");

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync($"/api/v1/reports/sla/compliance?tenantId=tenant-b&ticketType={(int)TicketType.Request}&fromUtc={from}&toUtc={to}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("tenant-b", fake.LastComplianceTenantId);
    }

    [Fact]
    public async Task Admin_CanQueryAllTenants_WhenTenantIdMissing()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("RUN_MIGRATIONS", "false");

        var fake = new FakeReportingService();
        using var factory = CreateFactory(fake);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");

        var response = await client.GetAsync("/api/v1/reports/sla/breached?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(fake.LastBreachedTenantId);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeReportingService fake)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
            builder.UseEnvironment("Production");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISlaReportingQueryService>(fake);
                services.RemoveAll<ICurrentUserAccessService>();
                services.AddSingleton<ICurrentUserAccessService, SlaReportTestAccessService>();
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "Test";
                    o.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, SlaReportTestAuthHandler>("Test", _ => { });
            });
        });
    }

    private sealed class FakeReportingService : ISlaReportingQueryService
    {
        public string? LastComplianceTenantId { get; private set; }
        public string? LastBreachedTenantId { get; private set; }

        public Task<SlaComplianceSummaryDto> GetComplianceSummaryAsync(SlaComplianceQuery query, CancellationToken ct)
        {
            LastComplianceTenantId = query.TenantId;
            return Task.FromResult(new SlaComplianceSummaryDto
            {
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc
            });
        }

        public Task<PagedResult<SlaTicketRowDto>> GetBreachedTicketsAsync(SlaTicketListQuery query, CancellationToken ct)
        {
            LastBreachedTenantId = query.TenantId;
            return Task.FromResult(new PagedResult<SlaTicketRowDto>());
        }

        public Task<PagedResult<SlaTicketRowDto>> GetNearBreachTicketsAsync(SlaNearBreachQuery query, CancellationToken ct)
        {
            return Task.FromResult(new PagedResult<SlaTicketRowDto>());
        }

        public Task<PagedResult<SlaCompletedRowDto>> GetCompletedTicketsAsync(SlaCompletedQuery query, CancellationToken ct)
        {
            return Task.FromResult(new PagedResult<SlaCompletedRowDto>());
        }
    }

    private sealed class SlaReportTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public SlaReportTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var auth = Request.Headers.Authorization.ToString();
            var role = auth.Contains("HelpdeskAdmin", StringComparison.OrdinalIgnoreCase)
                ? "HelpdeskAdmin"
                : auth.Contains("ScopedTechnician", StringComparison.OrdinalIgnoreCase)
                    ? "ScopedTechnician"
                : "Technician";

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "test-user"),
                new(ClaimTypes.Role, role)
            };

            if (!string.Equals(role, "HelpdeskAdmin", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim("sla_test_tenant", "tenant-a"));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class SlaReportTestAccessService : ICurrentUserAccessService
    {
        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            var roles = user.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var isAdmin = roles.Contains(HelpdeskPermissions.HelpdeskAdmin);
            var organizationId = user.FindFirst("sla_test_tenant")?.Value;
            var isScopedTechnician = roles.Contains("ScopedTechnician");
            var permissions = roles.Contains("Technician") || isScopedTechnician
                ? HelpdeskPermissions.TechnicalBundle.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scopedGrants = isScopedTechnician
                ? HelpdeskPermissions.TechnicalBundle
                    .Select(permission => new ScopedPermissionGrant(permission, "tenant-b"))
                    .ToHashSet()
                : new HashSet<ScopedPermissionGrant>();
            var allowedOrganizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                allowedOrganizations.Add(organizationId);
            }
            if (isScopedTechnician)
            {
                allowedOrganizations.Add("tenant-b");
            }

            var profile = new CurrentUserAccessProfile(
                true,
                user.Identity?.Name,
                null,
                organizationId,
                null,
                null,
                isAdmin,
                roles,
                permissions,
                allowedOrganizations,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            {
                ScopedPermissionGrants = scopedGrants
            };
            return Task.FromResult(profile);
        }
    }
}
