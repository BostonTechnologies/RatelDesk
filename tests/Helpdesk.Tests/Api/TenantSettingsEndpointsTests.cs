using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Users;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Api;

public sealed class TenantSettingsEndpointsTests
{
    [Fact]
    public async Task Settings_permission_is_scoped_and_updates_only_safe_fields_with_audit()
    {
        await using var host = await CreateHostAsync();
        var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tenant-admin/organizations/a/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/tenant-admin/organizations/b/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/tenant-admin/organizations/b/settings", new { name = "Escalated" })).StatusCode);
        var response = await client.PutAsJsonAsync("/api/v1/tenant-admin/organizations/a/settings", new
        {
            name = "  Updated A  ", contactInfo = "  help@example.test ", itSupportOrganizationId = "attacker", orchestrationTenantId = 999
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        var organization = await db.Organizations.SingleAsync(item => item.Id == "a");
        Assert.Equal("Updated A", organization.Name);
        Assert.Equal("help@example.test", organization.ContactInfo);
        Assert.Equal("support", organization.ItSupportOrganizationId);
        Assert.Equal(42, organization.OrchestrationTenantId);
        Assert.Equal("B", (await db.Organizations.SingleAsync(item => item.Id == "b")).Name);
        Assert.Contains(await db.ActivityLogs.ToListAsync(), item => item.RelatedEntityId == "a" && item.UserId == "settings-admin");
    }

    [Fact]
    public async Task Settings_only_administrator_can_discover_its_tenant_but_not_other_memberships()
    {
        await using var host = await CreateHostAsync();
        var client = host.GetTestClient();
        var organizations = await client.GetFromJsonAsync<TenantAdministrationEndpoints.TenantOrganizationResponse[]>("/api/v1/tenant-admin/organizations?permission=Tenant.Settings.Manage");
        Assert.Equal("a", Assert.Single(organizations!).Id);
        var response = await client.PutAsJsonAsync("/api/v1/tenant-admin/organizations/a/settings", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<WebApplication> CreateHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddSingleton<ITenantContext>(Substitute.For<ITenantContext>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddDbContext<RatelDeskIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName + "-identity"));
        builder.Services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<RatelDeskIdentityDbContext>();
        var access = new CurrentUserAccessProfile(true, "Settings administrator", "admin@example.test", "a", "A", null, false,
            new HashSet<string>(), new HashSet<string> { HelpdeskPermissions.TenantSettingsManage },
            new HashSet<string> { "a", "b" }, new HashSet<string>())
        {
            UsesScopedPermissions = true,
            ScopedPermissionGrants = new HashSet<ScopedPermissionGrant> { new(HelpdeskPermissions.TenantSettingsManage, "a") }
        };
        var accessService = Substitute.For<ICurrentUserAccessService>();
        accessService.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(access);
        builder.Services.AddSingleton(accessService);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTenantAdministrationEndpoints();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            db.Organizations.AddRange(new Organization { Id = "a", Name = "A", ItSupportOrganizationId = "support", OrchestrationTenantId = 42 },
                new Organization { Id = "b", Name = "B" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "settings-admin")], "Test")), "Test")));
    }
}
