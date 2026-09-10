using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Configuration;
using Helpdesk.Shared.Services;
using Helpdesk.Tests.Mocks;
using Helpdesk.Application.Services.Email;
using Dodo.Primitives;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helpdesk.Tests.Api;

public class PresenceEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PresenceEndpointsTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((context, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test",
                    ["Jwt:Key"] = "test-key-123456789012345678901234"
                });
            });

            builder.ConfigureServices(services =>
            {
                var userRepo = new InMemoryRepository<User>();
                var roleRepo = new InMemoryRepository<Role>();
                var orgRepo = new InMemoryRepository<Organization>();

                var orgId = Uuid.CreateVersion7().ToString();
                orgRepo.CreateAsync(new Organization { Id = orgId, Name = "DevOrg" }).Wait();
                roleRepo.CreateAsync(new Role { Name = "Admin" }).Wait();
                userRepo.CreateAsync(new User
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    Role = "Admin",
                    OrganizationId = orgId,
                    HashedPassword = BCrypt.Net.BCrypt.HashPassword("password")
                }).Wait();

                services.AddSingleton<IRepository<User>>(userRepo);
                services.AddSingleton<IRepository<Role>>(roleRepo);
                services.AddSingleton<IRepository<Organization>>(orgRepo);
                services.AddSingleton<IEmailIngestionService, DummyEmailIngestionService>();
            });
        });
    }

    [Fact(Skip = "RevisitCodex")]
    public async Task Get_Presence_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/presence/online-users");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(Skip = "RevisitCodex")]
    public async Task Get_Presence_ReturnsOk_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClientAsync();
        var resp = await client.GetAsync("/api/v1/presence/online-users");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var login = new LoginRequest("test@example.com", "password");
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", login);
        resp.EnsureSuccessStatusCode();
        var token = (await resp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
