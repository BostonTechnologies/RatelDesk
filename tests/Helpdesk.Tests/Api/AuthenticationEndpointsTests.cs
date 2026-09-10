using System.Net;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Helpdesk.Tests.Mocks;
using Helpdesk.Application.Services.Email;
using Dodo.Primitives;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Helpdesk.Tests.Api;

public class AuthenticationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginEndpoint_NotAvailable_WhenNotDevelopment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                var userRepo = new InMemoryRepository<User>();
                var roleRepo = new InMemoryRepository<Role>();
                var orgRepo = new InMemoryRepository<Organization>();

                var orgId = Uuid.CreateVersion7().ToString();
                orgRepo.CreateAsync(new Organization { Id = orgId, Name = "DevOrg" }).Wait();
                roleRepo.CreateAsync(new Role { Name = "Admin" }).Wait();
                userRepo.CreateAsync(new User
                {
                    Name = "Prod User",
                    Email = "prod@example.com",
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

        var client = prodFactory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("prod@example.com", "password"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

