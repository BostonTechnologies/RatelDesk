using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Helpdesk.Infrastructure.Identity;

public static class LocalIdentityDatabaseInitializer
{
    public static async Task EnsureSqliteSchemaAsync(
        RatelDeskIdentityDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlite())
        {
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUsers'";
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
            {
                var creator = db.GetService<IRelationalDatabaseCreator>();
                await creator.CreateTablesAsync(cancellationToken);
            }
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }
}
