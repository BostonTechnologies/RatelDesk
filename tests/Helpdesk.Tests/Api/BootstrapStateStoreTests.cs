using Helpdesk.API.Bootstrap;
using Helpdesk.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Tests.Api;

public sealed class BootstrapStateStoreTests
{
    [Fact]
    public async Task PostgreSql_preflight_rejects_a_missing_connection_without_attempting_setup()
    {
        var preflight = new PostgreSqlSetupPreflightService();

        var result = await preflight.CheckAsync(null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("connection string", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_admin_recovery_does_not_create_a_missing_sqlite_database()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-recovery-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "missing.db");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:Sqlite:Path"] = databasePath,
                ["DataProtection:KeyRingPath"] = Path.Combine(directory, "keys")
            }).Build();

            var token = await LocalAdminRecoveryCommand.GenerateActivationTokenAsync(configuration, "admin@example.test");

            Assert.Null(token);
            Assert.False(File.Exists(databasePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Generated_operator_code_unlocks_a_new_unconfigured_descriptor()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions { StateDirectory = directory });
            var descriptor = await store.LoadOrCreateAsync();
            var setupCode = await File.ReadAllTextAsync(Path.Combine(directory, "setup-code"));
            var sessions = new BootstrapSessionService();

            Assert.Equal(BootstrapState.Unconfigured, descriptor.State);
            Assert.NotEqual(setupCode.Trim(), descriptor.SetupCodeHash);
            Assert.False(sessions.TryCreate(descriptor, "incorrect", out _));
            Assert.True(sessions.TryCreate(descriptor, setupCode, out var session));
            Assert.True(sessions.IsValid(session, descriptor));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Rotating_the_setup_code_invalidates_existing_setup_sessions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions { StateDirectory = directory });
            var descriptor = await store.LoadOrCreateAsync();
            var originalCode = await File.ReadAllTextAsync(Path.Combine(directory, "setup-code"));
            var sessions = new BootstrapSessionService();
            Assert.True(sessions.TryCreate(descriptor, originalCode, out var session));

            var rotatedCode = await store.RotateSetupCodeAsync();
            var rotatedDescriptor = await store.LoadOrCreateAsync();

            Assert.NotNull(rotatedCode);
            Assert.NotEqual(originalCode.Trim(), rotatedCode);
            Assert.False(sessions.IsValid(session, rotatedDescriptor));
            Assert.False(sessions.TryCreate(rotatedDescriptor, originalCode, out _));
            Assert.True(sessions.TryCreate(rotatedDescriptor, rotatedCode, out _));
            Assert.Equal(rotatedCode, (await File.ReadAllTextAsync(Path.Combine(directory, "setup-code"))).Trim());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Setup_code_cannot_be_rotated_after_setup_is_ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions { StateDirectory = directory });
            await store.LoadOrCreateAsync();
            await store.UpdateAsync(current => current with { State = BootstrapState.Ready });

            Assert.Null(await store.RotateSetupCodeAsync());
            Assert.False(File.Exists(Path.Combine(directory, "setup-code")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Caller_supplied_code_is_hashed_without_creating_a_retrieval_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions
            {
                StateDirectory = directory,
                SetupCode = "operator-provided-code"
            });

            var descriptor = await store.LoadOrCreateAsync();
            var updated = await store.UpdateAsync(current => current with
            {
                State = BootstrapState.Configuring,
                Provider = "Sqlite",
                OperationId = Guid.NewGuid()
            });

            Assert.Equal(BootstrapState.Configuring, updated.State);
            Assert.Equal("Sqlite", updated.Provider);
            Assert.NotEqual("operator-provided-code", descriptor.SetupCodeHash);
            Assert.False(File.Exists(Path.Combine(directory, "setup-code")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Sqlite_initialization_creates_the_first_instance_administrator_and_marks_ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(directory, "data");
        try
        {
            var options = new BootstrapOptions
            {
                StateDirectory = directory,
                DataDirectory = dataDirectory,
                SetupCode = "operator-provided-code"
            };
            var store = new FileBootstrapStateStore(options);
            await store.LoadOrCreateAsync();
            var configured = await store.UpdateAsync(current => current with
            {
                State = BootstrapState.Configuring,
                Provider = "Sqlite",
                SqlitePath = Path.Combine(dataDirectory, "rateldesk.db"),
                OperationId = Guid.NewGuid()
            });
            var dataProtection = DataProtectionProvider.Create(
                new DirectoryInfo(Path.Combine(directory, "keys")),
                configuration => configuration.SetApplicationName("Helpdesk-Keyring"));
            var initializer = new BootstrapInitializationService(store, options, dataProtection);

            var result = await initializer.InitializeAsync(configured, new FirstAdministratorRequest(
                "admin@example.test",
                "Instance Admin",
                "correct horse battery staple",
                "Example Organization",
                "Example Desk",
                "https://desk.example.test"), CancellationToken.None);

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(BootstrapState.Ready, result.Descriptor!.State);
            Assert.False(File.Exists(Path.Combine(directory, "setup-code")));

            var identityOptions = new DbContextOptionsBuilder<RatelDeskIdentityDbContext>()
                .UseSqlite($"Data Source={Path.Combine(dataDirectory, "rateldesk.db")}")
                .Options;
            await using var identity = new RatelDeskIdentityDbContext(identityOptions);
            Assert.True(await identity.Users.AnyAsync(user => user.Email == "admin@example.test" && user.IsInstanceAdministrator));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
