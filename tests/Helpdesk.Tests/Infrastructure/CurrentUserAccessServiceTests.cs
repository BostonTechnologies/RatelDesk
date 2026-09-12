using System.Security.Claims;
using Helpdesk.Infrastructure.Auth.Rbac;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Auth;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests.Infrastructure;

public class CurrentUserAccessServiceTests
{
    [Fact]
    public async Task Linked_active_customer_gets_default_user_permissions_and_tenant()
    {
        await using var db = CreateDb();
        var organization = new Organization { Id = "org-alpha", Name = "Alpha Organization" };
        var customer = new Customer { Id = "customer-primary", Name = "Primary User", Email = "primary@example.com", OrganizationId = organization.Id };
        db.Organizations.Add(organization);
        db.Customers.Add(customer);
        db.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            CustomerId = customer.Id,
            OidcIssuer = "https://id.example.com/application/o/rateldesk",
            OidcSubject = "subject-1",
            AuthentikEmail = customer.Email,
            InviteStatus = CustomerInviteStatus.Active
        });
        await db.SaveChangesAsync();

        var access = await new CurrentUserAccessService(db).ResolveAsync(User(customer.Email, "subject-1"));

        Assert.Equal(customer.Id, access.CustomerId);
        Assert.Equal(organization.Id, access.PrimaryOrganizationId);
        Assert.Contains(HelpdeskRoleBundles.User, access.RoleBundles);
        Assert.Contains(HelpdeskPermissions.SelfServiceUser, access.Permissions);
        Assert.Contains(HelpdeskPermissions.IncidentUser, access.Permissions);
        Assert.Contains(HelpdeskPermissions.RequestUser, access.Permissions);
    }

    [Fact]
    public async Task Technical_user_gets_msp_managed_tenants()
    {
        await using var db = CreateDb();
        db.Organizations.AddRange(
            new Organization { Id = "org-support", Name = "Support Organization" },
            new Organization { Id = "org-alpha", Name = "Alpha Organization", ItSupportOrganizationId = "org-support" });
        db.Customers.Add(new Customer { Id = "customer-tech", Name = "Technician", Email = "tech@example.com", OrganizationId = "org-support" });
        db.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            CustomerId = "customer-tech",
            OidcIssuer = "https://id.example.com/application/o/rateldesk",
            OidcSubject = "tech-subject",
            AuthentikEmail = "tech@example.com",
            InviteStatus = CustomerInviteStatus.Active
        });
        await db.SaveChangesAsync();

        var access = await new CurrentUserAccessService(db).ResolveAsync(User("tech@example.com", "tech-subject", AuthentikRbacGroups.Technical));

        Assert.Contains("org-support", access.AllowedOrganizationIds);
        Assert.Contains("org-alpha", access.AllowedOrganizationIds);
        Assert.Contains("org-alpha", access.ManagedOrganizationIds);
        Assert.Contains(HelpdeskPermissions.ChangeManager, access.Permissions);
    }

    [Fact]
    public async Task Client_admin_group_does_not_treat_identity_provider_tenant_as_application_scope()
    {
        await using var db = CreateDb();

        var access = await new CurrentUserAccessService(db).ResolveAsync(User(
            "admin@example.com",
            "client-admin-subject",
            "org-alpha",
            [AuthentikRbacGroups.ClientAdmin]));

        Assert.Contains(HelpdeskRoleBundles.DataManagementAdmin, access.RoleBundles);
        Assert.Contains(HelpdeskPermissions.DataManagementAdmin, access.Permissions);
        Assert.Empty(access.AllowedOrganizationIds);
        Assert.False(access.IsHelpdeskAdmin);
    }

    [Fact]
    public async Task Email_match_without_a_verified_identity_link_does_not_grant_access()
    {
        await using var db = CreateDb();
        var organization = new Organization { Id = "org-alpha", Name = "Alpha Organization" };
        var customer = new Customer { Id = "customer-primary", Name = "Primary User", Email = "primary@example.com", OrganizationId = organization.Id };
        db.Organizations.Add(organization);
        db.Customers.Add(customer);
        db.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            CustomerId = customer.Id,
            OidcIssuer = "https://id.example.com/application/o/rateldesk",
            OidcSubject = "subject-1",
            AuthentikEmail = customer.Email,
            InviteStatus = CustomerInviteStatus.Active
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, customer.Email), new Claim("email", customer.Email)],
            "test"));

        var access = await new CurrentUserAccessService(db).ResolveAsync(principal);

        Assert.Null(access.CustomerId);
        Assert.Empty(access.AllowedOrganizationIds);
        Assert.Empty(access.Permissions);
    }

    [Fact]
    public async Task Disabled_customer_gets_no_tenant_permissions()
    {
        await using var db = CreateDb();
        db.Organizations.Add(new Organization { Id = "org-alpha", Name = "Alpha Organization" });
        db.Customers.Add(new Customer
        {
            Id = "customer-primary",
            Name = "Primary User",
            Email = "primary@example.com",
            OrganizationId = "org-alpha",
            State = Helpdesk.Shared.Models.EntityState.Blocked
        });
        db.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            CustomerId = "customer-primary",
            OidcIssuer = "https://id.example.com/application/o/rateldesk",
            OidcSubject = "subject-1",
            AuthentikEmail = "primary@example.com",
            InviteStatus = CustomerInviteStatus.Active
        });
        await db.SaveChangesAsync();

        var access = await new CurrentUserAccessService(db).ResolveAsync(User("primary@example.com", "subject-1"));

        Assert.Null(access.CustomerId);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.AllowedOrganizationIds);
    }

    [Fact]
    public async Task Local_scoped_role_assignments_keep_permissions_in_their_assigned_organization()
    {
        await using var db = CreateDb();
        db.Organizations.AddRange(
            new Organization { Id = "org-a", Name = "Organization A" },
            new Organization { Id = "org-b", Name = "Organization B" });
        db.Users.Add(new User { Id = "local-user", Name = "Local user", Email = "local@example.test", OrganizationId = "org-a", Role = "User" });
        db.ScopedRoleAssignments.AddRange(
            new ScopedRoleAssignment { UserId = "local-user", OrganizationId = "org-a", RoleKey = ScopedRoleCatalog.Technician },
            new ScopedRoleAssignment { UserId = "local-user", OrganizationId = "org-b", RoleKey = ScopedRoleCatalog.SelfServiceUser });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "local-user"),
                new Claim(ClaimTypes.Email, "local@example.test"),
                new Claim("auth_mode", "local")
            ],
            "RatelDeskLocal"));

        var access = await new CurrentUserAccessService(db).ResolveAsync(principal);

        Assert.True(access.HasPermission(HelpdeskPermissions.IncidentManager, "org-a"));
        Assert.False(access.HasPermission(HelpdeskPermissions.IncidentManager, "org-b"));
        Assert.True(access.HasPermission(HelpdeskPermissions.IncidentUser, "org-b"));
    }

    [Fact]
    public async Task Local_scoped_role_assignments_for_disabled_organizations_are_ignored()
    {
        await using var db = CreateDb();
        db.Organizations.AddRange(
            new Organization { Id = "active-org", Name = "Active organization" },
            new Organization { Id = "disabled-org", Name = "Disabled organization", IsEnabled = false });
        db.Users.Add(new User { Id = "local-user", Name = "Local user", Email = "local@example.test", OrganizationId = "active-org", Role = "User" });
        db.ScopedRoleAssignments.Add(new ScopedRoleAssignment
        {
            UserId = "local-user",
            OrganizationId = "disabled-org",
            RoleKey = ScopedRoleCatalog.Technician
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "local-user"),
                new Claim("auth_mode", "local")
            ],
            "RatelDeskLocal"));

        var access = await new CurrentUserAccessService(db).ResolveAsync(principal);

        Assert.False(access.HasPermission(HelpdeskPermissions.IncidentManager, "disabled-org"));
        Assert.DoesNotContain("disabled-org", access.AllowedOrganizationIds);
    }

    [Fact]
    public async Task Persisted_custom_role_permissions_are_scoped_to_the_assigned_tenant()
    {
        await using var db = CreateDb();
        db.Organizations.AddRange(
            new Organization { Id = "org-a", Name = "Organization A" },
            new Organization { Id = "org-b", Name = "Organization B" });
        db.Users.Add(new User { Id = "local-user", Name = "Local user", Email = "local@example.test", OrganizationId = "org-a", Role = "User" });
        db.Roles.Add(new Role
        {
            Id = "custom-incident-reader",
            Key = "custom.incident-reader",
            Name = "Incident Reader",
            Scope = RoleScopeKind.Tenant,
            OwnerOrganizationId = "org-a",
            Permissions = [new RolePermission { Permission = HelpdeskPermissions.IncidentUser }]
        });
        db.ScopedRoleAssignments.AddRange(
            new ScopedRoleAssignment
            {
                UserId = "local-user",
                OrganizationId = "org-a",
                RoleKey = "custom.incident-reader"
            },
            new ScopedRoleAssignment
            {
                UserId = "local-user",
                OrganizationId = "org-b",
                RoleKey = "custom.incident-reader"
            },
            new ScopedRoleAssignment
            {
                UserId = "local-user",
                OrganizationId = "org-b",
                RoleKey = ScopedRoleCatalog.TenantAdministrator
            });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "local-user"),
                new Claim(ClaimTypes.Email, "local@example.test"),
                new Claim("auth_mode", "local")
            ],
            "RatelDeskLocal"));

        var access = await new CurrentUserAccessService(db).ResolveAsync(principal);

        Assert.True(access.HasPermission(HelpdeskPermissions.IncidentUser, "org-a"));
        Assert.False(access.HasPermission(HelpdeskPermissions.IncidentUser, "org-b"));
        Assert.False(access.HasPermission(HelpdeskPermissions.IncidentManager, "org-a"));
        Assert.True(access.HasPermission(HelpdeskPermissions.TenantUsersManage, "org-b"));
        Assert.False(access.HasPermission(HelpdeskPermissions.TenantUsersManage, "org-a"));
    }

    [Fact]
    public async Task Protected_built_in_role_definitions_seed_idempotently()
    {
        await using var db = CreateDb();

        await RoleDefinitionSeeder.EnsureBuiltInsAsync(db);
        await RoleDefinitionSeeder.EnsureBuiltInsAsync(db);

        var roles = await db.Roles.Include(role => role.Permissions).ToListAsync();
        Assert.Equal(RoleDefinitionCatalog.BuiltIns.Count, roles.Count);
        Assert.All(roles, role =>
        {
            Assert.True(role.IsBuiltIn);
            Assert.True(role.IsProtected);
        });
        Assert.Contains(roles, role =>
            role.Key == ScopedRoleCatalog.Technician &&
            role.Permissions.Select(permission => permission.Permission)
                .OrderBy(permission => permission)
                .SequenceEqual(HelpdeskPermissions.TechnicalBundle.OrderBy(permission => permission)));
    }

    private static ClaimsPrincipal User(string email, string subject, params string[] groups)
        => User(email, subject, tenantId: null, groups);

    private static ClaimsPrincipal User(string email, string subject, string? tenantId, string[] groups)
    {
        var claims = new List<Claim>
        {
            new("iss", "https://id.example.com/application/o/rateldesk/"),
            new("sub", subject),
            new("email", email),
            new(ClaimTypes.Email, email)
        };
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }
        claims.AddRange(groups.Select(group => new Claim("groups", group)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static HelpdeskDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HelpdeskDbContext(options, new EmptyTenantContext(), new HttpContextAccessor());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;
    }
}
