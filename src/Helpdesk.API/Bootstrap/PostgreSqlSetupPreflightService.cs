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
                    EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public'),
                    to_regclass('public."__EFMigrationsHistory"') IS NOT NULL;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var hasVector = reader.GetBoolean(0);
            var hasTrigram = reader.GetBoolean(1);
            var hasTables = reader.GetBoolean(2);
            var hasMigrationHistory = reader.GetBoolean(3);

            if (!hasVector || !hasTrigram)
            {
                return new PostgreSqlPreflightResult(false, "The target PostgreSQL database must have the vector and pg_trgm extensions installed.");
            }

            if (hasTables && !hasMigrationHistory)
            {
                return new PostgreSqlPreflightResult(false, "The selected PostgreSQL database is non-empty and is not recognized as a RatelDesk database.");
            }

            return new PostgreSqlPreflightResult(true, null);
        }
        catch (NpgsqlException)
        {
            return new PostgreSqlPreflightResult(false, "RatelDesk could not connect to the PostgreSQL database with the supplied settings.");
        }
    }
}

public sealed record PostgreSqlPreflightResult(bool Succeeded, string? Error)
{
    public static PostgreSqlPreflightResult InvalidConnection { get; } = new(false, "A valid PostgreSQL connection string is required.");
}
