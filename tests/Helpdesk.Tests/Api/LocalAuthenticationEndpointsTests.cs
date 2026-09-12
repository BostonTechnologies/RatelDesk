using System.Net;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Auth;
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
    private readonly InMemoryDatabaseRoot _identityDatabaseRoot = new();
    private readonly ServiceProvider _identityDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public LocalAuthenticationEndpointsTests()
    {
        var databaseName = $"local-auth-{Guid.NewGuid():N}";
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
                services.RemoveAll<DbContextOptions<RatelDeskIdentityDbContext>>();
                services.AddDbContext<RatelDeskIdentityDbContext>(options => options
                    .UseInMemoryDatabase(databaseName, _identityDatabaseRoot)
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
    public async Task Administrator_can_create_and_activate_a_local_account_with_a_single_use_token()
    {
        using var administratorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            "admin@example.test", "correct horse battery staple"))).StatusCode);

        var create = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/users", new LocalAuthenticationEndpoints.CreateLocalAccountRequest(
            "New Operator", "new.operator@example.test"));
        var activation = await create.Content.ReadFromJsonAsync<LocalAuthenticationEndpoints.LocalAccountActivationResponse>();

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(activation);
        Assert.False(string.IsNullOrWhiteSpace(activation.ActivationToken));
        await using var scope = _factory.Services.CreateAsyncScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        Assert.True(await domainDb.Users.AnyAsync(user => user.Id == activation.UserId && user.Email == activation.Email));

        var activate = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "another secure passphrase"));
        var replay = await administratorClient.PostAsJsonAsync("/api/v1/local-auth/activate", new LocalAuthenticationEndpoints.ActivateLocalAccountRequest(
            activation.Email, activation.ActivationToken, "a different secure passphrase"));
        using var accountClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await accountClient.PostAsJsonAsync("/api/v1/local-auth/login", new LocalAuthenticationEndpoints.LocalLoginRequest(
            activation.Email, "another secure passphrase"));

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }
}
