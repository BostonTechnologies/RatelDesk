using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.API.Authentication;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Api;

public sealed class IntegrationCredentialSqliteTests
{
    [Fact]
    public async Task List_endpoint_uses_provider_supported_chronological_ordering_with_a_stable_tie_breaker()
    {
        await using var harness = await Harness.CreateAsync();
        var older = Credential("owner", "older", DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        var firstAtSameInstant = Credential("owner", "first", DateTimeOffset.Parse("2026-01-02T00:00:00+00:00"));
        var secondAtSameInstant = Credential("owner", "second", firstAtSameInstant.CreatedAtUtc);
        secondAtSameInstant.Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
            identity.IntegrationCredentials.AddRange(older, firstAtSameInstant, secondAtSameInstant);
            await identity.SaveChangesAsync();
        }

        var response = await harness.Client.GetAsync("/api/v1/integration-credentials/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var credentials = await response.Content.ReadFromJsonAsync<IntegrationCredentialEndpoints.IntegrationCredentialMetadata[]>();
        Assert.NotNull(credentials);
        Assert.Equal(["second", "first", "older"], credentials.Select(credential => credential.Name));
    }

    [Fact]
    public async Task Forward_sqlite_migration_backfills_the_sort_key_for_existing_credentials()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RatelDeskIdentityDbContext>()
            .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly("Helpdesk.Infrastructure.SqliteMigrations"))
            .Options;
        var createdAtUtc = DateTimeOffset.Parse("2026-01-01T12:34:56.789+00:00");
        var credentialId = Guid.NewGuid();

        await using (var beforeUpgrade = new RatelDeskIdentityDbContext(options))
        {
            await beforeUpgrade.GetService<IMigrator>().MigrateAsync("20260917095318_AddIntegrationCredentials");
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO "IntegrationCredentials" (
                    "Id", "OwnerUserId", "Name", "Prefix", "SecretHash", "Purpose", "OrganizationId", "Permissions",
                    "ExpiresAtUtc", "CreatedAtUtc", "LastUsedAtUtc", "RevokedAtUtc")
                VALUES ($id, 'owner', 'existing', 'rdk_existing', 'hash', 'api', 'org-a', 'Incident.Read',
                    $expiresAt, $createdAt, NULL, NULL);
                """;
            insert.Parameters.AddWithValue("$id", credentialId.ToString());
            insert.Parameters.AddWithValue("$createdAt", createdAtUtc.ToString("O"));
            insert.Parameters.AddWithValue("$expiresAt", createdAtUtc.AddDays(30).ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }

        await using (var afterUpgrade = new RatelDeskIdentityDbContext(options))
            await afterUpgrade.Database.MigrateAsync();

        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT \"CreatedAtUnixMilliseconds\" FROM \"IntegrationCredentials\" WHERE \"Name\" = 'existing';";
        Assert.Equal(createdAtUtc.ToUnixTimeMilliseconds(), Convert.ToInt64(await verify.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Create_endpoint_pairs_an_mcp_credential_to_its_canonical_resource()
    {
        await using var harness = await Harness.CreateAsync();
        var response = await harness.Client.PostAsJsonAsync("/api/v1/integration-credentials/", new
        {
            name = "MCP client",
            purpose = "mcp",
            organizationId = "org-a",
            permissions = new[] { "Incident.Read" },
            lifetimeDays = 30,
            mcpResourceUri = "https://helpdesk.example/mcp/"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<IntegrationCredentialEndpoints.CreatedIntegrationCredential>();
        Assert.NotNull(created);
        Assert.Equal("https://helpdesk.example/mcp", created.McpResourceUri);
        Assert.StartsWith("rdk_", created.Secret, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        await using var scope = harness.Services.CreateAsyncScope();
        var credential = await scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>()
            .IntegrationCredentials.SingleAsync();
        Assert.Equal("mcp", credential.Purpose);
        Assert.Equal("https://helpdesk.example/mcp", credential.McpResourceUri);
    }

    [Fact]
    public async Task Create_endpoint_rejects_a_permission_from_another_organization()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/integration-credentials/", new
        {
            name = "Wrong organization",
            purpose = "api",
            organizationId = "org-b",
            permissions = new[] { "Incident.Write" },
            lifetimeDays = 30
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = harness.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>()
            .IntegrationCredentials.ToListAsync());
    }

    private static IntegrationCredential Credential(string ownerId, string name, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerId,
        Name = name,
        Prefix = $"rdk_{name}",
        SecretHash = "hash",
        Purpose = "api",
        OrganizationId = "org-a",
        Permissions = "Incident.Read",
        CreatedAtUtc = createdAtUtc,
        CreatedAtUnixMilliseconds = createdAtUtc.ToUnixTimeMilliseconds(),
        ExpiresAtUtc = createdAtUtc.AddDays(30)
    };

    private sealed class Harness(WebApplication application, SqliteConnection connection) : IAsyncDisposable
    {
        public IServiceProvider Services => application.Services;

        public HttpClient Client { get; } = application.GetTestClient();

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<RatelDeskIdentityDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<ITenantContext>(_ =>
            {
                var tenant = Substitute.For<ITenantContext>();
                tenant.IsHelpdeskAdmin.Returns(true);
                return tenant;
            });
            builder.Services.AddSingleton<ICurrentUserAccessService>(new TestAccessService());
            builder.Services.AddScoped<IIntegrationCredentialOwnerResolver, StaticOwnerResolver>();
            builder.Services.AddScoped<IAuthorizationHandler, IntegrationCredentialManagementSessionHandler>();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy(IntegrationCredentialEndpoints.CredentialManagementPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new IntegrationCredentialManagementSessionRequirement());
            }));
            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapIntegrationCredentialEndpoints();
            await application.StartAsync();
            await using (var scope = application.Services.CreateAsyncScope())
            {
                var identity = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
                await identity.Database.EnsureCreatedAsync();
                identity.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner", Email = "owner@example.test", IsEnabled = true });
                await identity.SaveChangesAsync();
                var domain = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await domain.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                domain.Organizations.AddRange(
                    new Organization { Id = "org-a", Name = "Organization A", IsEnabled = true },
                    new Organization { Id = "org-b", Name = "Organization B", IsEnabled = true });
                await domain.SaveChangesAsync();
            }

            return new Harness(application, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await application.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "owner"), new Claim("auth_mode", "local")],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class StaticOwnerResolver : IIntegrationCredentialOwnerResolver
    {
        public Task<IntegrationCredentialOwner?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => Task.FromResult<IntegrationCredentialOwner?>(new IntegrationCredentialOwner("owner"));
    }

    private sealed class TestAccessService : ICurrentUserAccessService
    {
        private static readonly CurrentUserAccessProfile Profile = new(
            true, "owner", "owner@example.test", "org-a", null, null, false,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["Incident.Read", "Incident.Write"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["org-a", "org-b"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase))
        {
            UsesScopedPermissions = true,
            ScopedPermissionGrants = new HashSet<ScopedPermissionGrant>
            {
                new("Incident.Read", "org-a"),
                new("Incident.Write", "org-a")
            }
        };

        public Task<CurrentUserAccessProfile> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
            => Task.FromResult(Profile);
    }
}
