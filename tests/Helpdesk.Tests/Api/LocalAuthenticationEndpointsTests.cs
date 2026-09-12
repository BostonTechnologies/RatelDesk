using System.Net;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Infrastructure.Identity;
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
            "Strong!Passw0rd");
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
            "Strong!Passw0rd"));

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Contains(login.Headers, header => string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase));

        var currentUser = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);
        var access = await currentUser.Content.ReadFromJsonAsync<CurrentUserAccessDto>();
        Assert.True(access!.IsHelpdeskAdmin);
    }
}
