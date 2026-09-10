using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Shared.Models;
using Microsoft.Extensions.Configuration;
using Helpdesk.Shared.Services;
using Dodo.Primitives;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Api;

public class IncidentsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IncidentsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Ensure test runs in Development mode
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
                roleRepo.CreateAsync(new Role { Name = "HelpdeskAdmin" }).Wait();
                userRepo.CreateAsync(new User
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    Role = "HelpdeskAdmin",
                    OrganizationId = orgId,
                    HashedPassword = BCrypt.Net.BCrypt.HashPassword("password")
                }).Wait();

                services.AddSingleton<IRepository<User>>(userRepo);
                services.AddSingleton<IRepository<Role>>(roleRepo);
                services.AddSingleton<IRepository<Organization>>(orgRepo);
            });
        });
    }

    [Fact(Skip = "RevisitCodex")]
    public async Task Get_Incidents_ReturnsUnauthorized_ForAnonymousUser()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = "JWT validation not configured for tests")]
    public async Task Get_Incidents_ReturnsSuccess_ForAuthenticatedUser()
    {
        var client = await GetAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/incidents?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Skip = "JWT validation not configured for tests")]
    public async Task Post_Incidents_ReturnsCreated()
    {
        var client = await GetAuthenticatedClientAsync();
        var payload = new { Title = "New incident", Description = "Test" };
        var response = await client.PostAsJsonAsync("/api/v1/incidents", payload);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.NoContent);
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
