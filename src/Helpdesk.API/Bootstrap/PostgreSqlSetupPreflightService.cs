using Npgsql;

namespace Helpdesk.API.Bootstrap;

public sealed class PostgreSqlSetupPreflightService
{
    public async Task<PostgreSqlPreflightResult> CheckAsync(string? connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return PostgreSqlPreflightResult.InvalidConnection;
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 5
            };
        }
        catch (ArgumentException)
        {
            return PostgreSqlPreflightResult.InvalidConnection;
        }

        try
        {
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector'),
                    EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm'),
                    COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public';
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var hasVector = reader.GetBoolean(0);
            var hasTrigram = reader.GetBoolean(1);
            var publicTableCount = reader.GetInt64(2);

            if (!hasVector || !hasTrigram)
            {
                return PostgreSqlPreflightResult.MissingExtensions;
            }

            if (publicTableCount == 0)
            {
                return PostgreSqlPreflightResult.EmptyTarget;
            }

            await reader.DisposeAsync();
            command.CommandText = """
                SELECT
                    to_regclass('public."__EFMigrationsHistory"') IS NOT NULL,
                    to_regclass('public."Organizations"') IS NOT NULL,
                    to_regclass('public."Users"') IS NOT NULL;
                """;
            await using var shapeReader = await command.ExecuteReaderAsync(cancellationToken);
            await shapeReader.ReadAsync(cancellationToken);
            var hasMigrationHistory = shapeReader.GetBoolean(0);
            var hasOrganizationsTable = shapeReader.GetBoolean(1);
            var hasUsersTable = shapeReader.GetBoolean(2);
            await shapeReader.DisposeAsync();

            if (!hasMigrationHistory)
            {
                return Classify(
                    hasVector,
                    hasTrigram,
                    publicTableCount,
                    hasMigrationHistory,
                    hasOrganizationsTable,
                    hasUsersTable,
                    hasInitialRatelDeskMigration: false);
            }

            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" LIKE '%InitPostgres');
                """;
            var hasInitialRatelDeskMigration = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
            return Classify(
                hasVector,
                hasTrigram,
                publicTableCount,
                hasMigrationHistory,
                hasOrganizationsTable,
                hasUsersTable,
                hasInitialRatelDeskMigration);
        }
        catch (NpgsqlException)
        {
            return new PostgreSqlPreflightResult(false, "RatelDesk could not connect to the PostgreSQL database with the supplied settings.", null);
        }
    }

    public static PostgreSqlPreflightResult Classify(
        bool hasVector,
        bool hasTrigram,
        long publicTableCount,
        bool hasMigrationHistory,
        bool hasOrganizationsTable,
        bool hasUsersTable,
        bool hasInitialRatelDeskMigration)
    {
        if (!hasVector || !hasTrigram)
        {
            return PostgreSqlPreflightResult.MissingExtensions;
        }

        if (publicTableCount == 0)
        {
            return PostgreSqlPreflightResult.EmptyTarget;
        }

        if (hasMigrationHistory && hasOrganizationsTable && hasUsersTable && hasInitialRatelDeskMigration)
        {
            return PostgreSqlPreflightResult.EstablishedRatelDesk;
        }

        return PostgreSqlPreflightResult.UnrelatedTarget;
    }
}

public enum PostgreSqlTargetKind
{
    Empty,
    EstablishedRatelDesk,
    UnrelatedOrUnrecognized
}

public sealed record PostgreSqlPreflightResult(bool Succeeded, string? Error, PostgreSqlTargetKind? Target)
{
    public static PostgreSqlPreflightResult InvalidConnection { get; } = new(false, "A valid PostgreSQL connection string is required.", null);
    public static PostgreSqlPreflightResult MissingExtensions { get; } = new(false, "The target PostgreSQL database must have the vector and pg_trgm extensions installed.", null);
    public static PostgreSqlPreflightResult EmptyTarget { get; } = new(true, null, PostgreSqlTargetKind.Empty);
    public static PostgreSqlPreflightResult EstablishedRatelDesk { get; } = new(false, "The selected PostgreSQL database is an established RatelDesk installation. Configure it at deployment instead of running first-time setup.", PostgreSqlTargetKind.EstablishedRatelDesk);
    public static PostgreSqlPreflightResult UnrelatedTarget { get; } = new(false, "The selected PostgreSQL database is non-empty and is not recognized as a RatelDesk database.", PostgreSqlTargetKind.UnrelatedOrUnrecognized);
}
