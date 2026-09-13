using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Helpdesk.Infrastructure.SqliteMigrations;

public sealed class SqliteHelpdeskDbContextFactory : IDesignTimeDbContextFactory<HelpdeskDbContext>
{
    public HelpdeskDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite("Data Source=rateldesk-design-time.db", sqlite =>
                sqlite.MigrationsAssembly(typeof(SqliteHelpdeskDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new HelpdeskDbContext(options, new MigrationTenantContext(), new HttpContextAccessor());
    }
}

public sealed class SqliteIdentityDbContextFactory : IDesignTimeDbContextFactory<RatelDeskIdentityDbContext>
{
    public RatelDeskIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RatelDeskIdentityDbContext>()
            .UseSqlite("Data Source=rateldesk-identity-design-time.db", sqlite =>
                sqlite.MigrationsAssembly(typeof(SqliteIdentityDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new RatelDeskIdentityDbContext(options);
    }
}

internal sealed class MigrationTenantContext : ITenantContext
{
    public string? TenantId => "migration";

    public string? UserId => null;

    public bool IsHelpdeskAdmin => true;
}
