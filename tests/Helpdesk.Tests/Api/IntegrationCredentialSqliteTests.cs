using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Helpdesk.API.Endpoints.Authentication;
using Helpdesk.API.Authentication;
using Helpdesk.Infrastructure.Identity;
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
            builder.Services.AddDbContext<RatelDeskIdentityDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddSingleton(Substitute.For<ICurrentUserAccessService>());
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
}
