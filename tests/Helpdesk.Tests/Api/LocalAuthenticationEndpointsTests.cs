using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.API.Endpoints.Users;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.DTOs.Auth;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helpdesk.Tests.Api;

public sealed class LocalAuthenticationEndpointsTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly InMemoryDatabaseRoot _applicationDatabaseRoot = new();
    private readonly ServiceProvider _applicationDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();
    private readonly InMemoryDatabaseRoot _identityDatabaseRoot = new();
    private readonly ServiceProvider _identityDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public LocalAuthenticationEndpointsTests()
    {
        var identityDatabaseName = $"local-auth-identity-{Guid.NewGuid():N}";
        var applicationDatabaseName = $"local-auth-application-{Guid.NewGuid():N}";
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("Authentication:Mode", "Local");
            builder.UseSetting("Authentication:AllowInsecureLocalhost", "true");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "Local",
                ["Authentication:AllowInsecureLocalhost"] = "true"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HelpdeskDbContext>>();
                services.AddDbContext<HelpdeskDbContext>(options => options
                    .UseInMemoryDatabase(applicationDatabaseName, _applicationDatabaseRoot)
                    .UseInternalServiceProvider(_applicationDatabaseProvider));
                services.RemoveAll<DbContextOptions<RatelDeskIdentityDbContext>>();
                services.AddDbContext<RatelDeskIdentityDbContext>(options => options
                    .UseInMemoryDatabase(identityDatabaseName, _identityDatabaseRoot)
                    .UseInternalServiceProvider(_identityDatabaseProvider));
            });
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(
            new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test", DisplayName = "Instance Admin", IsInstanceAdministrator = true },
            "correct horse battery staple");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _applicationDatabaseProvider.DisposeAsync();
        await _identityDatabaseProvider.DisposeAsync();
    }

    [Fact]
    public async Task Local_login_issues_a_cookie_that_authenticates_subsequent_api_requests()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var login = await client.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test",
            "correct horse battery staple"));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Contains(login.Headers, header => string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase));

        var currentUser = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);
        var access = await currentUser.Content.ReadFromJsonAsync<CurrentUserAccessDto>();
        Assert.True(access!.IsHelpdeskAdmin);
    }

    [Fact]
    public async Task Last_enabled_instance_administrator_cannot_be_disabled()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test",
            "correct horse battery staple"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var administrator = await users.FindByEmailAsync("admin@example.test");

        var disable = await client.PostAsync($"/api/v1/local-auth/users/{administrator!.Id}/disable", content: null);

        Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
        Assert.True((await users.FindByIdAsync(administrator.Id))!.IsEnabled);
    }

    [Fact]
    public async Task Disabled_local_account_loses_an_existing_cookie_session()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var operatorCreate = await users.CreateAsync(
            new ApplicationUser { UserName = "operator@example.test", Email = "operator@example.test", DisplayName = "Operator" },
            "correct horse battery staple");
        Assert.True(operatorCreate.Succeeded, string.Join(", ", operatorCreate.Errors.Select(error => error.Description)));
        var operatorUser = await users.FindByEmailAsync("operator@example.test");

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var operatorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await operatorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "operator@example.test", "correct horse battery staple"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser!.Id}/disable", content: null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await operatorClient.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_reenable_a_disabled_local_account()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var operatorCreate = await users.CreateAsync(
            new ApplicationUser { UserName = "reenable.operator@example.test", Email = "reenable.operator@example.test", DisplayName = "Reenable Operator" },
            "correct horse battery staple");
        Assert.True(operatorCreate.Succeeded, string.Join(", ", operatorCreate.Errors.Select(error => error.Description)));
        var operatorUser = await users.FindByEmailAsync("reenable.operator@example.test");

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser!.Id}/disable", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsync($"/api/v1/local-auth/users/{operatorUser.Id}/enable", content: null)).StatusCode);

        using var operatorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await operatorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "reenable.operator@example.test", "correct horse battery staple"));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.True((await users.FindByIdAsync(operatorUser.Id))!.IsEnabled);
    }

    [Fact]
    public async Task Administrator_can_create_and_activate_a_local_account_with_a_single_use_token()
    {
        const string organizationId = "local-account-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Local account organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "New Operator", "new.operator@example.test")
        {
            OrganizationId = organizationId,
            IsTestUser = true
        });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);
        Assert.False(string.IsNullOrWhiteSpace(activation.ActivationToken));
        await using var scope = _factory.Services.CreateAsyncScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        var domainUser = await domainDb.Users.SingleAsync(user => user.Id == activation.UserId && user.Email == activation.Email);
        Assert.Equal(organizationId, domainUser.OrganizationId);
        Assert.True(domainUser.IsTestUser);
        Assert.Contains(
            await domainDb.ScopedRoleAssignments.ToListAsync(),
            assignment => assignment.UserId == activation.UserId &&
                          assignment.OrganizationId == organizationId &&
                          assignment.RoleKey == ScopedRoleCatalog.SelfServiceUser);

        var activate = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "another secure passphrase"));
        var replay = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "a different secure passphrase"));
        using var accountClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await accountClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"));
        var access = await accountClient.GetFromJsonAsync<CurrentUserAccessDto>("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.NotNull(access);
        Assert.Equal(organizationId, access.PrimaryOrganizationId);
        Assert.Contains(organizationId, access.AllowedOrganizationIds);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.SelfServiceUser, access.Permissions);
        Assert.Contains(access.ScopedPermissionGrants, grant =>
            grant.OrganizationId == organizationId &&
            grant.Permission == HelpdeskPermissions.SelfServiceUser);
    }

    [Fact]
    public async Task Administrator_cannot_create_a_local_account_for_an_unknown_or_disabled_organization()
    {
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = "disabled-local-account-organization", Name = "Disabled organization", IsEnabled = false });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var unknownOrganization = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Unknown Organization Operator", "unknown.organization.operator@example.test")
        {
            OrganizationId = "missing-organization"
        });
        var disabledOrganization = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Disabled Organization Operator", "disabled.organization.operator@example.test")
        {
            OrganizationId = "disabled-local-account-organization"
        });

        Assert.Equal(HttpStatusCode.BadRequest, unknownOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, disabledOrganization.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("unknown.organization.operator@example.test"));
        Assert.Null(await users.FindByEmailAsync("disabled.organization.operator@example.test"));
    }

    [Fact]
    public async Task Administrator_cannot_create_a_scoped_local_account_without_an_organization()
    {
        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Unscoped Operator", "unscoped.operator@example.test"));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("unscoped.operator@example.test"));
    }

    [Fact]
    public async Task Administrator_can_assign_the_technician_bundle_to_a_local_account()
    {
        const string organizationId = "local-technician-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Local technician organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Local Technician", "local.technician@example.test")
        {
            OrganizationId = organizationId,
            Role = "Technician"
        });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);

        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation!.Email, activation.ActivationToken, "another secure passphrase"))).StatusCode);
        using var technicianClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await technicianClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"))).StatusCode);

        var access = await technicianClient.GetFromJsonAsync<CurrentUserAccessDto>("/api/v1/auth/me");

        Assert.NotNull(access);
        Assert.Equal(organizationId, access.PrimaryOrganizationId);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.IncidentManager, access.Permissions);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.RequestManager, access.Permissions);
        Assert.Contains(Helpdesk.Shared.Auth.HelpdeskPermissions.ChangeManager, access.Permissions);
    }

    [Fact]
    public async Task Administrator_can_replace_a_local_accounts_tenant_scoped_role_assignments()
    {
        const string firstOrganizationId = "first-scoped-role-organization";
        const string secondOrganizationId = "second-scoped-role-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.AddRange(
                new Organization { Id = firstOrganizationId, Name = "First scoped role organization" },
                new Organization { Id = secondOrganizationId, Name = "Second scoped role organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);
        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Scoped Operator", "scoped.operator@example.test")
        {
            OrganizationId = firstOrganizationId
        });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);

        var replace = await administratorClient.PutAsJsonAsync($"/api/v1/local-auth/users/{activation!.UserId}/assignments",
            new LocalAuthenticationEndpoints.ReplaceLocalScopedRoleAssignmentsRequest(
            [new LocalAuthenticationEndpoints.LocalScopedRoleAssignment(ScopedRoleCatalog.Technician, secondOrganizationId)]));
        var assignments = await administratorClient.GetFromJsonAsync<LocalAuthenticationEndpoints.LocalScopedRoleAssignmentsResponse>(
            $"/api/v1/local-auth/users/{activation.UserId}/assignments");

        Assert.Equal(HttpStatusCode.NoContent, replace.StatusCode);
        Assert.Equal([new LocalAuthenticationEndpoints.LocalScopedRoleAssignment(ScopedRoleCatalog.Technician, secondOrganizationId)], assignments!.Assignments);
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "another secure passphrase"))).StatusCode);
        using var accountClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await accountClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"))).StatusCode);
        var access = await accountClient.GetFromJsonAsync<CurrentUserAccessDto>("/api/v1/auth/me");

        Assert.Contains(access!.ScopedPermissionGrants, grant =>
            grant.OrganizationId == secondOrganizationId && grant.Permission == HelpdeskPermissions.IncidentManager);
        Assert.DoesNotContain(access.ScopedPermissionGrants, grant => grant.OrganizationId == firstOrganizationId);
    }

    [Fact]
    public async Task Role_definition_api_requires_an_administrator_and_protects_built_ins()
    {
        const string organizationId = "custom-role-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Custom role organization" });
            await setupDb.SaveChangesAsync();
        }

        using var anonymous = _factory.CreateClient();
        var anonymousList = await anonymous.GetAsync("/api/v1/admin/role-definitions/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var invalidWriter = await administratorClient.PostAsJsonAsync("/api/v1/admin/role-definitions/",
            new RoleDefinitionEndpoints.CreateRoleDefinitionRequest(
                "Incomplete Incident Writer",
                null,
                organizationId,
                [HelpdeskPermissions.IncidentManager]));
        var created = await administratorClient.PostAsJsonAsync("/api/v1/admin/role-definitions/",
            new RoleDefinitionEndpoints.CreateRoleDefinitionRequest(
                "Incident Reader",
                null,
                organizationId,
                [HelpdeskPermissions.IncidentUser]));
        var definitions = await administratorClient.GetFromJsonAsync<List<RoleDefinitionEndpoints.RoleDefinitionResponse>>(
            "/api/v1/admin/role-definitions/");
        var technician = Assert.Single(definitions!, definition => definition.Key == ScopedRoleCatalog.Technician);
        var deleteBuiltIn = await administratorClient.DeleteAsync($"/api/v1/admin/role-definitions/{technician.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, invalidWriter.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains(definitions!, definition =>
            definition.Key == "custom.incident-reader" &&
            definition.OwnerOrganizationId == organizationId &&
            definition.Permissions.SequenceEqual([HelpdeskPermissions.IncidentUser]));
        Assert.True(technician.IsBuiltIn);
        Assert.True(technician.IsProtected);
        Assert.Equal(HttpStatusCode.Conflict, deleteBuiltIn.StatusCode);
    }

    [Fact]
    public async Task Tenant_membership_route_allows_only_the_self_service_role()
    {
        const string organizationId = "tenant-membership-organization";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            setupDb.Organizations.Add(new Organization { Id = organizationId, Name = "Tenant membership organization" });
            await setupDb.SaveChangesAsync();
        }

        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);
        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Tenant Member", "tenant.member@example.test") { OrganizationId = organizationId });
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var route = $"/api/v1/tenant-admin/organizations/{organizationId}/users/{activation!.UserId}/assignments";
        var technician = await administratorClient.PutAsJsonAsync(route,
            new TenantAdministrationEndpoints.ReplaceTenantMembershipRequest([ScopedRoleCatalog.Technician]));
        var selfService = await administratorClient.PutAsJsonAsync(route,
            new TenantAdministrationEndpoints.ReplaceTenantMembershipRequest([ScopedRoleCatalog.SelfServiceUser]));
        var membership = await administratorClient.GetFromJsonAsync<TenantAdministrationEndpoints.TenantMembershipResponse>(route);

        Assert.Equal(HttpStatusCode.BadRequest, technician.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, selfService.StatusCode);
        Assert.Equal([ScopedRoleCatalog.SelfServiceUser], membership!.RoleKeys);
    }

    [Fact]
    public async Task Tenant_administrator_can_manage_membership_only_in_its_assigned_organization()
    {
        const string organizationId = "tenant-admin-organization";
        const string otherOrganizationId = "other-tenant-admin-organization";
        string tenantAdministratorId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var identityUsers = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var createTenantAdmin = await identityUsers.CreateAsync(
                new ApplicationUser { UserName = "tenant.admin@example.test", Email = "tenant.admin@example.test", DisplayName = "Tenant Admin" },
                "correct horse battery staple");
            Assert.True(createTenantAdmin.Succeeded);
            tenantAdministratorId = (await identityUsers.FindByEmailAsync("tenant.admin@example.test"))!.Id;

            var db = setupScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            db.Organizations.AddRange(
                new Organization { Id = organizationId, Name = "Tenant admin organization" },
                new Organization { Id = otherOrganizationId, Name = "Other tenant organization" });
            db.Users.Add(new User { Id = tenantAdministratorId, Name = "Tenant Admin", Email = "tenant.admin@example.test", OrganizationId = organizationId, Role = "User" });
            db.ScopedRoleAssignments.Add(new ScopedRoleAssignment { UserId = tenantAdministratorId, OrganizationId = organizationId, RoleKey = ScopedRoleCatalog.TenantAdministrator });
            await db.SaveChangesAsync();
        }

        using var instanceAdministrator = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await instanceAdministrator.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);
        var createTarget = await instanceAdministrator.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "Tenant Target", "tenant.target@example.test") { OrganizationId = organizationId });
        var target = await createTarget.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();
        await using (var targetScope = _factory.Services.CreateAsyncScope())
        {
            var db = targetScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            db.ScopedRoleAssignments.Add(new ScopedRoleAssignment
            {
                UserId = target!.UserId,
                OrganizationId = organizationId,
                RoleKey = ScopedRoleCatalog.Technician
            });
            await db.SaveChangesAsync();
        }

        using var tenantAdministrator = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await tenantAdministrator.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "tenant.admin@example.test", "correct horse battery staple"))).StatusCode);
        var ownRoute = $"/api/v1/tenant-admin/organizations/{organizationId}/users/{target!.UserId}/assignments";
        var otherRoute = $"/api/v1/tenant-admin/organizations/{otherOrganizationId}/users/{target.UserId}/assignments";
        var ownTenant = await tenantAdministrator.PutAsJsonAsync(ownRoute,
            new TenantAdministrationEndpoints.ReplaceTenantMembershipRequest([ScopedRoleCatalog.SelfServiceUser]));
        var otherTenant = await tenantAdministrator.PutAsJsonAsync(otherRoute,
            new TenantAdministrationEndpoints.ReplaceTenantMembershipRequest([ScopedRoleCatalog.SelfServiceUser]));
        var availableOrganizations = await tenantAdministrator.GetFromJsonAsync<List<TenantAdministrationEndpoints.TenantOrganizationResponse>>(
            "/api/v1/tenant-admin/organizations");
        var members = await tenantAdministrator.GetFromJsonAsync<List<TenantAdministrationEndpoints.TenantMemberResponse>>(
            $"/api/v1/tenant-admin/organizations/{organizationId}/users/");

        Assert.Equal(HttpStatusCode.NoContent, ownTenant.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherTenant.StatusCode);
        Assert.Collection(availableOrganizations!, organization => Assert.Equal(organizationId, organization.Id));
        var targetMembership = Assert.Single(members!, member => member.UserId == target.UserId);
        Assert.Equal([ScopedRoleCatalog.SelfServiceUser], targetMembership.RoleKeys);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var assignedRoles = await verificationScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>().ScopedRoleAssignments
            .Where(assignment => assignment.UserId == target.UserId && assignment.OrganizationId == organizationId)
            .Select(assignment => assignment.RoleKey)
            .ToListAsync();
        Assert.Contains(ScopedRoleCatalog.SelfServiceUser, assignedRoles);
        Assert.Contains(ScopedRoleCatalog.Technician, assignedRoles);
    }

    [Fact]
    public async Task Local_login_requires_an_authenticator_code_after_two_factor_is_enabled()
    {
        using var setupClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await setupClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var setup = await setupClient.PostAsync("/api/v1/local-auth/two-factor/setup", content: null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var setupResult = await setup.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.AuthenticatorSetupResponse>();
        Assert.False(string.IsNullOrWhiteSpace(setupResult?.SharedKey));

        var authenticatorCode = CreateTotp(setupResult!.SharedKey);
        Assert.Equal(HttpStatusCode.NoContent, (await setupClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var enable = await setupClient.PostAsJsonAsync("/api/v1/local-auth/two-factor/enable", new LocalAuthenticationEndpoints.EnableTwoFactorRequest(authenticatorCode));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var recoveryCodes = await enable.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.TwoFactorRecoveryCodesResponse>();
        Assert.Equal(10, recoveryCodes!.RecoveryCodes.Count);

        using var loginClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var missingCode = await loginClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"));
        var withAuthenticator = await loginClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple", TwoFactorCode: authenticatorCode));
        using var recoveryClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var withRecoveryCode = await recoveryClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple", TwoFactorCode: recoveryCodes.RecoveryCodes[0]));

        Assert.Equal(HttpStatusCode.Unauthorized, missingCode.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withAuthenticator.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withRecoveryCode.StatusCode);
    }

    private static string CreateTotp(string sharedKey)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var bitCount = 0;
        var bytes = new List<byte>();
        foreach (var character in sharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant())
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new ArgumentException("The authenticator key is not valid base32.", nameof(sharedKey));
            }

            bits = (bits << 5) | value;
            bitCount += 5;
            if (bitCount < 8)
            {
                continue;
            }

            bitCount -= 8;
            bytes.Add((byte)(bits >> bitCount));
            bits &= (1 << bitCount) - 1;
        }

        var counter = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Span<byte> counterBytes = stackalloc byte[8];
        for (var index = counterBytes.Length - 1; index >= 0; index--)
        {
            counterBytes[index] = (byte)counter;
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(bytes.ToArray(), counterBytes);
        var offset = hash[^1] & 0x0f;
        var value32 = ((hash[offset] & 0x7f) << 24) |
                      (hash[offset + 1] << 16) |
                      (hash[offset + 2] << 8) |
                      hash[offset + 3];
        return (value32 % 1_000_000).ToString("D6", global::System.Globalization.CultureInfo.InvariantCulture);
    }
}
