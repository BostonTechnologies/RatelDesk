using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Helpdesk.Infrastructure.Identity;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RatelDeskIdentityDbContext>
{
    public RatelDeskIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__HelpdeskDb")
            ?? "Host=localhost;Port=5432;Database=rateldesk;Username=rateldesk;Password=rateldesk";
        var options = new DbContextOptionsBuilder<RatelDeskIdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RatelDeskIdentityDbContext(options);
    }
}
