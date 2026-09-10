using System.Net.Http.Headers;
using System.Net.Http.Json;
using Helpdesk.API;
using Helpdesk.Shared.DTOs.Organization;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Dodo.Primitives;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Helpdesk.Tests.Api;

public class OrganizationCustomerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrganizationCustomerEndpointsTests(WebApplicationFactory<Program> factory)
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
                var orgSettingsRepo = new InMemoryRepository<OrganizationAiKbSettings>();
                var customerRepo = new InMemoryRepository<Customer>();
                var emailTemplateRepo = new InMemoryRepository<EmailTemplate>();

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
                services.AddSingleton<IRepository<OrganizationAiKbSettings>>(orgSettingsRepo);
                services.AddSingleton<IRepository<Customer>>(customerRepo);
                services.AddSingleton<IRepository<EmailTemplate>>(emailTemplateRepo);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });
    }

    [Fact]
    public async Task Organization_CreateEdit_PersistsIsEnabled()
    {
        var client = await GetAuthenticatedClientAsync();

        var managerResp = await client.PostAsJsonAsync("/api/v1/organizations", new Organization { Name = "MSP", IsEnabled = true });
        managerResp.EnsureSuccessStatusCode();
        var manager = await managerResp.Content.ReadFromJsonAsync<Organization>();

        var create = new Organization { Name = "Org1", IsEnabled = false };
        var createResp = await client.PostAsJsonAsync("/api/v1/organizations", create);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<Organization>();
        Assert.Equal(EntityState.Blocked, created!.State);

        var id = created.Id;
        created.IsEnabled = true;
        created.ItSupportOrganizationId = manager!.Id;
        var updateResp = await client.PutAsJsonAsync($"/api/v1/organizations/{id}", created);
        updateResp.EnsureSuccessStatusCode();

        var get = await client.GetFromJsonAsync<Organization>($"/api/v1/organizations/{id}");
        Assert.Equal(EntityState.Enabled, get!.State);
        Assert.Equal(manager.Id, get.ItSupportOrganizationId);

        get.ItSupportOrganizationId = null;
        var clearResp = await client.PutAsJsonAsync($"/api/v1/organizations/{id}", get);
        clearResp.EnsureSuccessStatusCode();

        var cleared = await client.GetFromJsonAsync<Organization>($"/api/v1/organizations/{id}");
        Assert.Null(cleared!.ItSupportOrganizationId);
    }

    [Fact]
    public async Task Customer_CreateEdit_PersistsIsEnabled()
    {
        var client = await GetAuthenticatedClientAsync();

        var orgResp = await client.PostAsJsonAsync("/api/v1/organizations", new Organization { Name = "Org2", IsEnabled = true });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<Organization>();

        var create = new Customer { Name = "Alice", Email = "a@b.com", OrganizationId = org!.Id, IsEnabled = false };
        var createResp = await client.PostAsJsonAsync("/api/v1/customers", create);
        createResp.EnsureSuccessStatusCode();
        var customer = await createResp.Content.ReadFromJsonAsync<Customer>();
        Assert.Equal(EntityState.Blocked, customer!.State);

        customer.IsEnabled = true;
        var updateResp = await client.PutAsJsonAsync($"/api/v1/customers/{customer.Id}", customer);
        updateResp.EnsureSuccessStatusCode();

        var get = await client.GetFromJsonAsync<Customer>($"/api/v1/customers/{customer.Id}");
        Assert.Equal(EntityState.Enabled, get!.State);
    }

    [Fact]
    public async Task Organization_AiKbSettings_PersistsSwitchesAndModelFields()
    {
        var client = await GetAuthenticatedClientAsync();

        var orgResp = await client.PostAsJsonAsync("/api/v1/organizations", new Organization { Name = "AI Org", IsEnabled = true });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<Organization>();

        var update = new OrganizationAiKbSettingsDto
        {
            EnableAiSearch = true,
            EnableAiAnswers = false,
            SearchThreshold = 0.42,
            AnswerThreshold = 0.73,
            EmbeddingProviderId = Guid.NewGuid().ToString(),
            EmbeddingModel = "text-embedding-3-small",
            EmbeddingDimensions = 1536,
            KnowledgeProviderId = Guid.NewGuid().ToString(),
            KnowledgeModelName = "qwen3.5:9b",
            SuggestionLimit = 4,
            AllowedServicesCsv = "incident,request",
            EnableProviderFallback = false,
            MaxProviderAttempts = 1,
            MinimumSuggestionFeedbackCount = 2,
            MinimumSuggestionHelpfulRate = 0.5,
            MinimumAutomationFeedbackCount = 1,
            MinimumAutomationResolvedRate = 0.25
        };

        var updateResp = await client.PutAsJsonAsync($"/api/v1/organizations/{org!.Id}/ai-kb-settings", update);
        updateResp.EnsureSuccessStatusCode();

        var get = await client.GetFromJsonAsync<OrganizationAiKbSettingsDto>($"/api/v1/organizations/{org.Id}/ai-kb-settings");

        Assert.NotNull(get);
        Assert.True(get!.EnableAiSearch);
        Assert.False(get.EnableAiAnswers);
        Assert.Equal(update.EmbeddingProviderId, get.EmbeddingProviderId);
        Assert.Equal(update.EmbeddingModel, get.EmbeddingModel);
        Assert.Equal(update.EmbeddingDimensions, get.EmbeddingDimensions);
        Assert.Equal(update.KnowledgeProviderId, get.KnowledgeProviderId);
        Assert.Equal(update.KnowledgeModelName, get.KnowledgeModelName);
        Assert.False(get.EnableProviderFallback);
        Assert.Equal(update.MaxProviderAttempts, get.MaxProviderAttempts);
    }

    private Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");
        return Task.FromResult(client);
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
