namespace Helpdesk.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? Provider { get; init; }

    public SqliteDatabaseOptions Sqlite { get; init; } = new();

    public DatabaseProvider ResolveProvider(string? legacyPostgreSqlConnectionString) => Provider?.Trim() switch
    {
        null or "" when !string.IsNullOrWhiteSpace(legacyPostgreSqlConnectionString) => DatabaseProvider.PostgreSql,
        null or "" => DatabaseProvider.Sqlite,
        var provider when string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) => DatabaseProvider.PostgreSql,
        var provider when string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) => DatabaseProvider.Sqlite,
        var provider => throw new InvalidOperationException($"Database:Provider '{provider}' is not supported. Use Sqlite or PostgreSql.")
    };
}

public sealed class SqliteDatabaseOptions
{
    public string Path { get; init; } = "/var/lib/rateldesk/data/rateldesk.db";

    // The initialized runtime must never silently replace a missing database.
    public bool CreateIfMissing { get; init; } = true;
}

public enum DatabaseProvider
{
    PostgreSql,
    Sqlite
}
