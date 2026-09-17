using System.Security.Claims;
using Helpdesk.API.Authentication;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Api;

public sealed class IntegrationCredentialManagementAuthorizationTests
{
    [Theory]
    [InlineData("integration")]
    [InlineData("mcp")]
    [InlineData("gateway")]
    public async Task Non_interactive_credentials_cannot_become_a_management_session(string authenticationMode)
    {
        await using var fixture = Fixture.Create();
        fixture.Identity.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner", IsEnabled = true });
        await fixture.Identity.SaveChangesAsync();
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, "owner"),
            new Claim("auth_mode", authenticationMode));

        Assert.Null(await fixture.Resolver.ResolveAsync(principal));
        Assert.False(await IsAuthorizedAsync(fixture.Resolver, principal));
    }

    [Fact]
    public async Task Local_enabled_account_can_manage_its_own_credentials()
    {
        await using var fixture = Fixture.Create();
        fixture.Identity.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner", IsEnabled = true });
        await fixture.Identity.SaveChangesAsync();
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, "owner"),
            new Claim("auth_mode", "local"));

        Assert.Equal("owner", (await fixture.Resolver.ResolveAsync(principal))?.UserId);
        Assert.True(await IsAuthorizedAsync(fixture.Resolver, principal));
    }

    [Fact]
    public async Task Linked_oidc_identity_resolves_the_persisted_application_account_not_the_provider_subject()
    {
        await using var fixture = Fixture.Create();
        fixture.Identity.Users.Add(new ApplicationUser { Id = "application-user", UserName = "application-user", IsEnabled = true });
        fixture.Application.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            Id = "link-1",
            CustomerId = "customer-1",
            OidcIssuer = "https://issuer.example",
            OidcSubject = "provider-subject",
            LocalAccountId = "application-user"
        });
        await fixture.Identity.SaveChangesAsync();
        await fixture.Application.SaveChangesAsync();
        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, "provider-subject"),
            new Claim("iss", "https://issuer.example/"),
            new Claim("sub", "provider-subject"));

        Assert.Equal("application-user", (await fixture.Resolver.ResolveAsync(principal))?.UserId);
        Assert.True(await IsAuthorizedAsync(fixture.Resolver, principal));
    }

    [Fact]
    public async Task Disabled_linked_account_cannot_manage_credentials()
    {
        await using var fixture = Fixture.Create();
        fixture.Identity.Users.Add(new ApplicationUser { Id = "disabled", UserName = "disabled", IsEnabled = false });
        fixture.Application.CustomerAuthLinks.Add(new CustomerAuthLink
        {
            Id = "link-2",
            CustomerId = "customer-2",
            OidcIssuer = "https://issuer.example",
            OidcSubject = "provider-subject",
            LocalAccountId = "disabled"
        });
        await fixture.Identity.SaveChangesAsync();
        await fixture.Application.SaveChangesAsync();

        Assert.Null(await fixture.Resolver.ResolveAsync(Principal(
            new Claim("iss", "https://issuer.example"),
            new Claim("sub", "provider-subject"))));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));

    private static async Task<bool> IsAuthorizedAsync(IIntegrationCredentialOwnerResolver resolver, ClaimsPrincipal principal)
    {
        var context = new AuthorizationHandlerContext(
            [new IntegrationCredentialManagementSessionRequirement()], principal, resource: null);
        await new IntegrationCredentialManagementSessionHandler(resolver).HandleAsync(context);
        return context.HasSucceeded;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(HelpdeskDbContext application, RatelDeskIdentityDbContext identity)
        {
            Application = application;
            Identity = identity;
            Resolver = new IntegrationCredentialOwnerResolver(application, identity);
        }

        public HelpdeskDbContext Application { get; }

        public RatelDeskIdentityDbContext Identity { get; }

        public IntegrationCredentialOwnerResolver Resolver { get; }

        public static Fixture Create()
        {
            var application = new HelpdeskDbContext(
                new DbContextOptionsBuilder<HelpdeskDbContext>().UseInMemoryDatabase($"credentials-app-{Guid.NewGuid():N}").Options,
                Substitute.For<ITenantContext>(),
                new HttpContextAccessor());
            var identity = new RatelDeskIdentityDbContext(
                new DbContextOptionsBuilder<RatelDeskIdentityDbContext>().UseInMemoryDatabase($"credentials-identity-{Guid.NewGuid():N}").Options);
            return new Fixture(application, identity);
        }

        public async ValueTask DisposeAsync()
        {
            await Application.DisposeAsync();
            await Identity.DisposeAsync();
        }
    }
}
