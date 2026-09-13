using System.Data.Common;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Helpdesk.API.Bootstrap;

/// <summary>Checks installation evidence before migrations or any SQLite file creation.</summary>
public sealed class BootstrapStartupService(
    IBootstrapStateStore store,
    BootstrapOptions options,
    IDataProtectionProvider protection)
{
    public async Task<BootstrapDescriptor> ResolveAsync(IConfiguration configuration, CancellationToken token = default)
    {
        await using var lease = await BootstrapOperationLease.AcquireAsync(options.StateDirectory, token);
        var descriptor = await store.LoadOrCreateAsync(token);
        var connection = configuration.GetConnectionString("HelpdeskDb");
        var explicitSqlitePath = configuration["Database:Sqlite:Path"];
        var configuredProvider = configuration["Database:Provider"];
        var provider = !string.IsNullOrWhiteSpace(configuredProvider) ? configuredProvider :
            !string.IsNullOrWhiteSpace(connection) ? "PostgreSql" : descriptor.Provider ?? "Sqlite";
        provider = string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ? "PostgreSql" :
            string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : provider;
        var sqlitePath = Path.GetFullPath(explicitSqlitePath ?? descriptor.SqlitePath ?? Path.Combine(options.DataDirectory, "rateldesk.db"));
        try
        {
            if (descriptor.ProtectedKeyRingProof is not null &&
                !BootstrapKeyRingProof.IsValid(protection, descriptor))
                return await RecoveryAsync(token);
            var runtimeSecretPath = Path.Combine(options.StateDirectory, "image-signing-secret");
            if (File.Exists(runtimeSecretPath))
                _ = protection.CreateProtector("RatelDesk.Bootstrap.ImageSigningSecret.v1")
                    .Unprotect(await File.ReadAllTextAsync(runtimeSecretPath, token));

            string? previousConnection = null;
            if (descriptor.ProtectedPostgreSqlConnection is not null)
                previousConnection = protection.CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                    .Unprotect(descriptor.ProtectedPostgreSqlConnection);
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(connection)) connection = previousConnection;
                if (string.IsNullOrWhiteSpace(connection))
                    return descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired
                        ? await RecoveryAsync(token)
                        : await store.UpdateAsync(current => current with { Provider = "PostgreSql" }, token);
            }
            else if (!string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return await RecoveryAsync(token);
            }

            var selectionChanged = descriptor.Provider != provider ||
                (provider == "Sqlite" ? descriptor.SqlitePath != sqlitePath : connection != previousConnection);
            if (descriptor.State == BootstrapState.Configuring && selectionChanged)
            {
                var prior = await ReconcileSelectedMarkerAsync(store, descriptor, protection, token);
                if (prior is not null) descriptor = prior;
                if (descriptor.State == BootstrapState.RecoveryRequired) return descriptor;
            }
            var evidence = await InspectAsync(provider, sqlitePath, connection, token);
            var expectsMarker = descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired;
            if (expectsMarker && provider == "Sqlite" && descriptor.ProtectedKeyRingProof is null)
            {
                // Older rc.4 descriptors predate the proof. They may be upgraded
                // only with their surviving key ring, never with a newly empty one.
                var keyDirectory = configuration["DataProtection:KeyRingPath"] ?? Path.Combine(options.StateDirectory, "keys");
                if (!Directory.Exists(keyDirectory) || !Directory.EnumerateFiles(keyDirectory, "key-*.xml").Any())
                    return await RecoveryAsync(token);
            }
            if (evidence.Marker is { } marker)
            {
                if (marker.InstanceId != descriptor.InstanceId || marker.OperationId != descriptor.OperationId)
                    return await RecoveryAsync(token);
                return await store.UpdateAsync(current => current with
                {
                    State = BootstrapState.Ready, CompletedAtUtc = marker.CompletedAtUtc,
                    Provider = provider,
                    SqlitePath = provider == "Sqlite" ? sqlitePath : null,
                    ProtectedPostgreSqlConnection = provider == "PostgreSql"
                        ? connection == previousConnection ? current.ProtectedPostgreSqlConnection
                            : protection.CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1").Protect(connection!)
                        : null,
                    ProtectedKeyRingProof = current.ProtectedKeyRingProof ?? BootstrapKeyRingProof.Create(protection, current.InstanceId)
                }, token);
            }

            if (expectsMarker || evidence.Kind == TargetKind.Unrecognized)
                return await RecoveryAsync(token);

            if (evidence.Kind == TargetKind.Legacy)
            {
                // Only established, positively identified deployment-managed PostgreSQL
                // is adopted. A surviving rc.4 marker without its descriptor needs recovery.
                if (provider != "PostgreSql" || string.IsNullOrWhiteSpace(configuration.GetConnectionString("HelpdeskDb")))
                    return await RecoveryAsync(token);
                var settings = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSql" }).Build();
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddHelpdeskInfrastructure(settings);
                await using var servicesProvider = services.BuildServiceProvider();
                await using var scope = servicesProvider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
                await db.Database.MigrateAsync(token);
                await scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>().Database.MigrateAsync(token);
                var adoption = await new LegacyInstallationAdoptionService().AdoptAsync(db, token);
                if (adoption == LegacyInstallationAdoptionResult.NotEstablished)
                    return await RecoveryAsync(token);
                db.ChangeTracker.Clear();
                var adopted = await db.InstanceInitializations.SingleAsync(token);
                return await store.UpdateAsync(current => current with
                {
                    InstanceId = adopted.InstanceId, OperationId = adopted.OperationId,
                    Provider = "PostgreSql", State = BootstrapState.Ready,
                    ProtectedPostgreSqlConnection = protection.CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1").Protect(connection!),
                    CompletedAtUtc = adopted.CompletedAtUtc, AdoptedLegacy = true,
                    ProtectedKeyRingProof = BootstrapKeyRingProof.Create(protection, adopted.InstanceId)
                }, token);
            }

            // No installation exists. Deployment settings prepare storage, but never
            // create an administrator or turn schema existence into readiness.
            if (!string.IsNullOrWhiteSpace(connection) || !string.IsNullOrWhiteSpace(explicitSqlitePath))
            {
                return await store.UpdateAsync(current => current with
                {
                    State = BootstrapState.Configuring, Provider = provider,
                    SqlitePath = provider == "Sqlite" ? sqlitePath : null,
                    ProtectedPostgreSqlConnection = provider == "PostgreSql"
                        ? connection == previousConnection ? current.ProtectedPostgreSqlConnection
                            : protection.CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1").Protect(connection!)
                        : null,
                    OperationId = current.Provider == provider &&
                        (provider == "Sqlite" ? current.SqlitePath == sqlitePath : connection == previousConnection)
                        ? current.OperationId ?? Guid.NewGuid() : Guid.NewGuid(),
                    ProtectedKeyRingProof = current.ProtectedKeyRingProof ?? BootstrapKeyRingProof.Create(protection, current.InstanceId)
                }, token);
            }
            return descriptor;
        }
        catch (Exception exception) when (exception is DbException or IOException or System.Security.Cryptography.CryptographicException or ArgumentException or FormatException)
        {
            // Keep credentials and provider exception details out of anonymous setup responses.
            return await RecoveryAsync(token);
        }
    }

    // Called with the operation lease held before replacing a selected target.
    internal static async Task<BootstrapDescriptor?> ReconcileSelectedMarkerAsync(
        IBootstrapStateStore store, BootstrapDescriptor descriptor,
        IDataProtectionProvider protection, CancellationToken token)
    {
        if (descriptor.State != BootstrapState.Configuring || descriptor.OperationId is null || descriptor.Provider is null)
            return null;
        try
        {
            var connection = descriptor.ProtectedPostgreSqlConnection is null ? null : protection
                .CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1").Unprotect(descriptor.ProtectedPostgreSqlConnection);
            if (descriptor.Provider == "PostgreSql" && string.IsNullOrWhiteSpace(connection)) return null;
            if (descriptor.Provider == "Sqlite" && string.IsNullOrWhiteSpace(descriptor.SqlitePath)) return null;
            var evidence = await InspectAsync(descriptor.Provider, descriptor.SqlitePath ?? string.Empty, connection, token);
            if (evidence.Marker is not { } marker) return null;
            var matches = marker.InstanceId == descriptor.InstanceId && marker.OperationId == descriptor.OperationId;
            return await store.UpdateAsync(current => current with
            {
                State = matches ? BootstrapState.Ready : BootstrapState.RecoveryRequired,
                CompletedAtUtc = matches ? marker.CompletedAtUtc : current.CompletedAtUtc
            }, token);
        }
        catch (Exception error) when (error is DbException or IOException or System.Security.Cryptography.CryptographicException or ArgumentException or FormatException)
        {
            return await store.UpdateAsync(current => current with { State = BootstrapState.RecoveryRequired }, token);
        }
    }

    private Task<BootstrapDescriptor> RecoveryAsync(CancellationToken token) =>
        store.UpdateAsync(current => current with { State = BootstrapState.RecoveryRequired }, token);

    internal static async Task<TargetEvidence> InspectAsync(string provider, string path, string? connectionString, CancellationToken token)
    {
        var sqlite = string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
        if (sqlite && !File.Exists(path)) return new(TargetKind.Empty, null);
        await using DbConnection connection = sqlite
            ? new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString())
            : new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 5, CommandTimeout = 5 }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sqlite
            ? "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
            : "SELECT table_name FROM information_schema.tables WHERE table_schema='public'";
        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
        if (tables.Contains("InstanceInitializations"))
        {
            command.CommandText = "SELECT \"InstanceId\", \"OperationId\", \"CompletedAtUtc\" FROM \"InstanceInitializations\"";
            await using var reader = await command.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                var marker = new InstanceInitialization
                {
                    InstanceId = Guid.Parse(reader.GetValue(0).ToString()!),
                    OperationId = Guid.Parse(reader.GetValue(1).ToString()!),
                    CompletedAtUtc = reader.GetValue(2) switch
                    {
                        DateTimeOffset instant => instant.ToUniversalTime(),
                        DateTime instant => new DateTimeOffset(instant.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(instant, DateTimeKind.Utc) : instant).ToUniversalTime(),
                        var value => DateTimeOffset.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture)
                    }
                };
                if (await reader.ReadAsync(token)) return new(TargetKind.Unrecognized, null);
                return new(TargetKind.Initialized, marker);
            }
        }
        if (tables.Count == 0) return new(TargetKind.Empty, null);
        if (!tables.Contains("__EFMigrationsHistory") || !tables.Contains("Users") || !tables.Contains("Organizations"))
            return new(TargetKind.Unrecognized, null);
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('20250811184655_InitPostgres', '20260912104238_InitialSqliteApplication')";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) == 0) return new(TargetKind.Unrecognized, null);
        command.CommandText = "SELECT COUNT(*) FROM \"Users\" u JOIN \"Organizations\" o ON u.\"OrganizationId\" = o.\"Id\" WHERE u.\"Email\" IS NOT NULL AND trim(u.\"Email\") <> ''";
        var users = Convert.ToInt64(await command.ExecuteScalarAsync(token));
        command.CommandText = "SELECT COUNT(*) FROM \"Organizations\"";
        var organizations = Convert.ToInt64(await command.ExecuteScalarAsync(token));
        if (users > 0 && organizations > 0) return new(TargetKind.Legacy, null);
        // Recognized schema after an interrupted initialization may be empty.
        command.CommandText = "SELECT COUNT(*) FROM \"Users\"";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) > 0 || organizations > 0)
            return new(TargetKind.Unrecognized, null);
        foreach (var table in new[] { "AspNetUsers", "Customers", "Tickets", "WorkLogs", "TicketTimelineEvents", "Attachments" })
        {
            if (!tables.Contains(table)) continue;
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) > 0) return new(TargetKind.Unrecognized, null);
        }
        return new(TargetKind.Empty, null, HasApplicationSchema: true);
    }

    internal enum TargetKind { Empty, Initialized, Legacy, Unrecognized }
    internal sealed record TargetEvidence(TargetKind Kind, InstanceInitialization? Marker, bool HasApplicationSchema = false);
}
