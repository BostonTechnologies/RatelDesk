using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.DTOs.AI;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Dodo.Primitives;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;

namespace Helpdesk.Tests.Api;

public class AiProviderEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AiProviderEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IAiProviderService, InMemoryAiProviderService>();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });
    }

    [Fact]
    public async Task Get_Providers_ReturnsUnauthorized_ForAnonymous()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/ai/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Provider_ReturnsNotFound_WhenMissing()
    {
        var client = await GetAuthenticatedClientAsync();
        var resp = await client.GetAsync($"/api/v1/ai/providers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Providers_ReturnsForbidden_ForNonAdmin()
    {
        var client = await GetAuthenticatedClientAsync("User");
        var resp = await client.GetAsync("/api/v1/ai/providers");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Crud_Endpoints_ReturnSuccess()
    {
        var client = await GetAuthenticatedClientAsync();
        var createDto = new AiProviderCreateDto
        {
            Name = "OpenAI",
            ProviderType = AiProviderType.OpenAICompatible,
            BaseUrl = "https://example.com",
            ApiKey = "key",
            IsEnabled = true
        };
        var createResp = await client.PostAsJsonAsync("/api/v1/ai/providers", createDto);
        var created = await createResp.Content.ReadFromJsonAsync<AiProviderSummaryDto>();
        var id = Guid.Parse(created!.Id);

        var getResp = await client.GetAsync($"/api/v1/ai/providers/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var updateDto = new AiProviderUpdateDto
        {
            Name = "New",
            ProviderType = AiProviderType.OpenAICompatible,
            BaseUrl = "https://example.com",
            IsEnabled = false
        };
        var putResp = await client.PutAsJsonAsync($"/api/v1/ai/providers/{id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var updated = await putResp.Content.ReadFromJsonAsync<AiProviderSummaryDto>();
        Assert.False(updated!.IsEnabled);

        var deleteResp = await client.DeleteAsync($"/api/v1/ai/providers/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task TestEndpoints_ReturnSuccess()
    {
        var client = await GetAuthenticatedClientAsync();
        var dto = new AiProviderCreateDto
        {
            Name = "OpenAI",
            ProviderType = AiProviderType.OpenAICompatible,
            BaseUrl = "https://example.com",
            ApiKey = "key",
            IsEnabled = true
        };
        var createResp = await client.PostAsJsonAsync("/api/v1/ai/providers", dto);
        var created = await createResp.Content.ReadFromJsonAsync<AiProviderSummaryDto>();
        var id = Guid.Parse(created!.Id);

        var testResp = await client.PostAsync($"/api/v1/ai/providers/{id}/test", null);
        Assert.Equal(HttpStatusCode.OK, testResp.StatusCode);
        var testResult = await testResp.Content.ReadFromJsonAsync<AiProviderTestResultDto>();
        Assert.NotNull(testResult);
        Assert.True(testResult!.Success);

        var chatResp = await client.PostAsJsonAsync($"/api/v1/ai/providers/{id}/chat-test", new AiChatRequestDto { Prompt = "hi" });
        Assert.Equal(HttpStatusCode.OK, chatResp.StatusCode);
    }

    [Fact]
    public async Task Test_Endpoint_ReturnsNotFound_WhenMissing()
    {
        var client = await GetAuthenticatedClientAsync();
        var resp = await client.PostAsync($"/api/v1/ai/providers/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    private Task<HttpClient> GetAuthenticatedClientAsync(string role = "HelpdeskAdmin")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", role);
        return Task.FromResult(client);
    }

    private class InMemoryAiProviderService : IAiProviderService
    {
        private readonly List<AiProvider> _providers = new();

        public Task<string> ChatTestAsync(Guid providerId, string prompt, string? model, CancellationToken token) => Task.FromResult("ok");

        public Task<AiProvider> CreateAsync(AiProvider provider, string apiKey, CancellationToken token)
        {
            provider.Id = provider.Id == Guid.Empty ? Guid.NewGuid() : provider.Id;
            _providers.Add(provider);
            return Task.FromResult(provider);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken token)
        {
            var removed = _providers.RemoveAll(p => p.Id == id) > 0;
            return Task.FromResult(removed);
        }

        public Task<AiProvider?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(_providers.FirstOrDefault(p => p.Id == id));

        public Task<IEnumerable<AiProvider>> ListAsync(CancellationToken token) => Task.FromResult<IEnumerable<AiProvider>>(_providers);

        public Task<IEnumerable<string>> ListModelsAsync(Guid providerId, CancellationToken token) => Task.FromResult<IEnumerable<string>>(new[] { "model" });

        public Task<AiProviderConnectionTestResult> TestAsync(Guid providerId, CancellationToken token)
            => Task.FromResult(new AiProviderConnectionTestResult(true, "https://example.com/api/models", 200, "Connection OK."));

        public Task<AiProvider?> UpdateAsync(AiProvider provider, string? apiKey, CancellationToken token)
        {
            var existing = _providers.FirstOrDefault(p => p.Id == provider.Id);
            if (existing is null) return Task.FromResult<AiProvider?>(null);
            existing.Name = provider.Name;
            existing.ProviderType = provider.ProviderType;
            existing.BaseUrl = provider.BaseUrl;
            existing.IsEnabled = provider.IsEnabled;
            existing.DefaultModel = provider.DefaultModel;
            existing.ExtraHeadersJson = provider.ExtraHeadersJson;
            return Task.FromResult<AiProvider?>(existing);
        }
    }

    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header))
                return Task.FromResult(AuthenticateResult.Fail("No authorization header"));

            var parts = header.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var role = parts.Length > 1 ? parts[1] : string.Empty;

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test"),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
