using Helpdesk.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql;

namespace Helpdesk.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HelpdeskDbContext>
{
    public HelpdeskDbContext CreateDbContext(string[] args)
    {
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); // Opt in to Npgsql's dynamic JSON (https://www.npgsql.org/doc/types/json.html)
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var optionsBuilder = new DbContextOptionsBuilder<HelpdeskDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("HELPDESK_DESIGNTIME_DB")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__HelpdeskDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:HelpdeskDb")
            ?? throw new InvalidOperationException(
                "A design-time Helpdesk database connection string is required. Set HELPDESK_DESIGNTIME_DB, ConnectionStrings__HelpdeskDb, or ConnectionStrings:HelpdeskDb.");

        optionsBuilder.UseNpgsql(connectionString, npg => npg.UseVector());

        var tenant = new DummyTenantContext();
        return new HelpdeskDbContext(optionsBuilder.Options, tenant, new Microsoft.AspNetCore.Http.HttpContextAccessor());
    }

    private class DummyTenantContext : ITenantContext
    {
        public string? TenantId => "design";
        public string? UserId => null;
        public bool IsHelpdeskAdmin => true;

    }
}
