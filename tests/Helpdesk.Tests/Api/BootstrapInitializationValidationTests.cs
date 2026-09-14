using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.API.Bootstrap;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Api;

public sealed class BootstrapInitializationValidationTests
{
    private static FirstAdministratorRequest ValidRequest => new(
        "first.admin@example.test", "First Administrator", "a sufficiently long test passphrase",
        "Test Organization", "RatelDesk Test", "https://helpdesk.example.test", "Africa/Johannesburg");

    [Theory]
    [InlineData("timeZoneId")]
    [InlineData("applicationUrl")]
    [InlineData("email")]
    [InlineData("displayName")]
    [InlineData("organizationName")]
    [InlineData("supportEmail")]
    [InlineData("supportUrl")]
    [InlineData("logoUrl")]
    [InlineData("compactLogoUrl")]
    public async Task Invalid_field_is_identified_before_creating_the_database(string field)
    {
        await using var harness = await Harness.CreateAsync();
        var request = field switch
        {
            "timeZoneId" => ValidRequest with { TimeZoneId = "Invalid/TimeZone" },
            "applicationUrl" => ValidRequest with { ApplicationUrl = "https://name:private-value@example.test" },
            "email" => ValidRequest with { Email = "not-an-email" },
            "displayName" => ValidRequest with { DisplayName = " " },
            "organizationName" => ValidRequest with { OrganizationName = " " },
            "supportEmail" => ValidRequest with { SupportEmail = "not-an-email" },
            "supportUrl" => ValidRequest with { SupportUrl = "file:///private" },
            "logoUrl" => ValidRequest with { LogoUrl = "file:///private" },
            _ => ValidRequest with { CompactLogoUrl = "file:///private" }
        };
        var result = await harness.Initializer.InitializeAsync(harness.Descriptor, request, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("setup_validation_failed", result.Code);
        Assert.Equal(field, Assert.Single(result.Errors!).Key);
        Assert.DoesNotContain(request.Password, JsonSerializer.Serialize(result));
        Assert.False(File.Exists(harness.DatabasePath));
        Assert.Equal(BootstrapState.Configuring, (await harness.Store.LoadOrCreateAsync(CancellationToken.None)).State);
    }

    [Theory]
    [InlineData("Africa/Johannesburg")]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    public async Task Corrected_time_zone_initializes_once_and_is_persisted(string timeZone)
    {
        await using var harness = await Harness.CreateAsync();
        var invalid = await harness.Initializer.InitializeAsync(harness.Descriptor,
            ValidRequest with { TimeZoneId = "Invalid/TimeZone" }, CancellationToken.None);
        Assert.False(invalid.Succeeded);
        var result = await harness.Initializer.InitializeAsync(harness.Descriptor,
            ValidRequest with { TimeZoneId = timeZone }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(BootstrapState.Ready, result.Descriptor!.State);
        await harness.AssertInitializedAsync(timeZone);
    }

    [Fact]
    public async Task Deployment_managed_time_zone_is_validated_instead_of_the_submitted_value()
    {
        await using var harness = await Harness.CreateAsync("Invalid/ManagedZone");
        var result = await harness.Initializer.InitializeAsync(harness.Descriptor, ValidRequest, CancellationToken.None);
        Assert.Equal("timeZoneId", Assert.Single(result.Errors!).Key);
        Assert.False(File.Exists(harness.DatabasePath));
    }

    [Fact]
    public async Task Valid_deployment_managed_time_zone_is_persisted()
    {
        await using var harness = await Harness.CreateAsync("Africa/Johannesburg");
        var result = await harness.Initializer.InitializeAsync(harness.Descriptor,
            ValidRequest with { TimeZoneId = "Invalid/SubmittedZone" }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        await harness.AssertInitializedAsync("Africa/Johannesburg");
    }

    [Fact]
    public async Task Password_policy_rejection_is_field_specific_and_correction_does_not_leave_an_orphan()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.Initializer.InitializeAsync(harness.Descriptor,
            ValidRequest with { Password = "short" }, CancellationToken.None);
        Assert.Equal("password", Assert.Single(result.Errors!).Key);
        Assert.Contains("15", result.Errors["password"][0]);
        var corrected = await harness.Initializer.InitializeAsync(harness.Descriptor, ValidRequest, CancellationToken.None);
        Assert.True(corrected.Succeeded, corrected.Error);
        await harness.AssertInitializedAsync("Africa/Johannesburg");
    }

    [Fact]
    public async Task Initialize_endpoint_returns_safe_validation_problem_and_preserves_the_setup_session()
    {
        await using var harness = await Harness.CreateAsync();
        await using var app = await harness.StartHostAsync();
        using var client = app.GetTestClient();
        var payload = ValidRequest with { TimeZoneId = "Invalid/TimeZone" };
        using var anonymous = await client.PostAsJsonAsync("/api/v1/setup/initialize", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.True(harness.Sessions.TryCreate(harness.Descriptor, harness.Options.SetupCode, out var session));
        client.DefaultRequestHeaders.Add("X-RatelDesk-Setup-Session", session);
        using var response = await client.PostAsJsonAsync("/api/v1/setup/initialize", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(text);
        Assert.Equal("setup_validation_failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Contains("time zone", problem.RootElement.GetProperty("errors").GetProperty("timeZoneId")[0].GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(payload.Password, text);
        Assert.DoesNotContain(payload.Email, text);
        Assert.DoesNotContain(session!, text);
        Assert.True(harness.Sessions.IsValid(session, await harness.Store.LoadOrCreateAsync(CancellationToken.None)));
        Assert.False(File.Exists(harness.DatabasePath));
    }

    [Fact]
    public async Task Runtime_failure_returns_a_reference_without_disclosing_the_database_path()
    {
        await using var harness = await Harness.CreateAsync();
        // A directory cannot be opened as a SQLite database. Validation passes;
        // the real storage failure must not masquerade as a passphrase rejection.
        Directory.CreateDirectory(harness.DatabasePath);
        await using var app = await harness.StartHostAsync();
        using var client = app.GetTestClient();
        Assert.True(harness.Sessions.TryCreate(harness.Descriptor, harness.Options.SetupCode, out var session));
        client.DefaultRequestHeaders.Add("X-RatelDesk-Setup-Session", session);
        using var response = await client.PostAsJsonAsync("/api/v1/setup/initialize", ValidRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(text);
        Assert.Equal("setup_initialization_failed", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(harness.DatabasePath, text);
        Assert.DoesNotContain(ValidRequest.Password, text);
        Assert.False(problem.RootElement.TryGetProperty("errors", out _));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "rateldesk-setup-validation-" + Guid.NewGuid().ToString("N"));
        public BootstrapOptions Options { get; }
        public FileBootstrapStateStore Store { get; }
        public BootstrapDescriptor Descriptor { get; private set; } = null!;
        public BootstrapInitializationService Initializer { get; }
        public BootstrapSessionService Sessions { get; }
        public string DatabasePath => Path.Combine(Options.DataDirectory, "rateldesk.db");
        private IDataProtectionProvider Protection { get; }

        private Harness(string? managedTimeZone)
        {
            Options = new BootstrapOptions
            {
                StateDirectory = Path.Combine(_directory, "bootstrap"),
                DataDirectory = Path.Combine(_directory, "data"),
                SetupCode = "validation-only-setup-code",
                Interactive = new BootstrapInteractiveOptions { TimeZoneId = managedTimeZone }
            };
            Directory.CreateDirectory(Options.DataDirectory);
            Store = new FileBootstrapStateStore(Options);
            Protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_directory, "keys")));
            Initializer = new BootstrapInitializationService(Store, Options, Protection);
            Sessions = new BootstrapSessionService();
        }

        public static async Task<Harness> CreateAsync(string? managedTimeZone = null)
        {
            var harness = new Harness(managedTimeZone);
            await harness.Store.LoadOrCreateAsync(CancellationToken.None);
            harness.Descriptor = await harness.Store.UpdateAsync(descriptor => descriptor with
            {
                State = BootstrapState.Configuring,
                Provider = "Sqlite",
                SqlitePath = harness.DatabasePath,
                OperationId = Guid.NewGuid()
            }, CancellationToken.None);
            return harness;
        }

        public async Task<WebApplication> StartHostAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(Options);
            builder.Services.AddSingleton<IBootstrapStateStore>(Store);
            builder.Services.AddSingleton(Protection);
            builder.Services.AddSingleton(Sessions);
            builder.Services.AddSingleton(Initializer);
            builder.Services.AddSingleton<PostgreSqlSetupPreflightService>();
            var app = builder.Build();
            app.MapBootstrapEndpoints();
            await app.StartAsync();
            return app;
        }

        public async Task AssertInitializedAsync(string timeZone)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:Sqlite:Path"] = DatabasePath,
                ["Authentication:Mode"] = "Local",
                ["StorageOptions:RootPath"] = Path.Combine(_directory, "storage")
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHelpdeskInfrastructure(configuration);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            var identity = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
            Assert.Equal(timeZone, (await db.InstanceInitializations.SingleAsync()).TimeZoneId);
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await identity.Users.CountAsync());
            Assert.True((await identity.Users.SingleAsync()).IsInstanceAdministrator);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
