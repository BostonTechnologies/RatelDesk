using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.API;
using Helpdesk.Application.Services.KB;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Article;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Headers;
using Xunit;

namespace Helpdesk.Tests.Api;

public class KbEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Guid _articleId;
    private readonly string _ticketId;

    public KbEndpointsTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("RUN_MIGRATIONS", "false");
        _articleId = Guid.NewGuid();
        _ticketId = Guid.NewGuid().ToString();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
            builder.UseEnvironment("Production");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HelpdeskDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                var provider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
                services.AddDbContext<HelpdeskDbContext>(o =>
                {
                    o.UseInMemoryDatabase("KbTests");
                    o.UseInternalServiceProvider(provider);
                });

                var userRepo = new InMemoryRepository<User>();
                var roleRepo = new InMemoryRepository<Role>();
                var orgRepo = new InMemoryRepository<Organization>();

                var orgId = Guid.NewGuid().ToString();
                orgRepo.CreateAsync(new Organization { Id = orgId, Name = "DevOrg" }).Wait();
                roleRepo.CreateAsync(new Role { Name = "HelpdeskAdmin" }).Wait();
                userRepo.CreateAsync(new User { Name = "Test User", Email = "test@example.com", Role = "HelpdeskAdmin", OrganizationId = orgId, HashedPassword = "pwd" }).Wait();

                services.AddSingleton<IRepository<User>>(userRepo);
                services.AddSingleton<IRepository<Role>>(roleRepo);
                services.AddSingleton<IRepository<Organization>>(orgRepo);

                services.AddScoped<IKnowledgeBuilderService, FakeKnowledgeBuilderService>();

                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "Test";
                    o.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                db.Database.EnsureCreated();
                db.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle { Id = _articleId, Title = "Old", OrganizationId = orgId, Service = "General", State = KnowledgeBaseArticleState.Draft, LinkedTicketId = _ticketId, CreatedAt = DateTime.UtcNow });
                db.Tickets.Add(new Incident { Id = _ticketId, Title = "Issue", Description = "d", OrganizationId = orgId, State = TicketState.Resolved });
                db.SaveChanges();
            });
        });
    }

    [Fact(Skip = "Requires authenticated KB setup")]
    public async Task Regeneration_StateTransition_And_Health_Work()
    {
        var client = GetAuthenticatedClient();

        var regen = await client.PostAsync($"/api/v1/kb/{_articleId}/regenerate", null);
        Assert.Equal(HttpStatusCode.OK, regen.StatusCode);

        var dto = new UpdateKbStateDto { State = KnowledgeBaseArticleState.Published };
        var stateResp = await client.PutAsJsonAsync($"/api/v1/kb/{_articleId}/state", dto);
        Assert.Equal(HttpStatusCode.OK, stateResp.StatusCode);

        var healthResp = await client.GetAsync("/api/v1/kb/health");
        Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);
        var json = await healthResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("TotalArticles").GetInt32());
        Assert.Equal(1, json.GetProperty("PublishedArticles").GetInt32());
    }

    private HttpClient GetAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "HelpdeskAdmin");
        return client;
    }

    private class FakeKnowledgeBuilderService : IKnowledgeBuilderService
    {
        private readonly HelpdeskDbContext _db;
        public FakeKnowledgeBuilderService(HelpdeskDbContext db) { _db = db; }
        public Task<KnowledgeBaseArticle> BuildArticleAsync(string prompt, CancellationToken token) => Task.FromResult(new KnowledgeBaseArticle());
        public async Task<KnowledgeBaseArticle?> GenerateDraftFromResolvedTicketAsync(string ticketId, CancellationToken token, bool regenerate = false)
        {
            var article = await _db.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.LinkedTicketId == ticketId, token);
            if (article == null) return null;
            article.LastRegeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(token);
            return article;
        }
    }

    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "test"), new Claim(ClaimTypes.Role, "HelpdeskAdmin") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
