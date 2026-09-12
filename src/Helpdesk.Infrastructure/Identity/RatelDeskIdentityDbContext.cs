using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Identity;

public sealed class RatelDeskIdentityDbContext(DbContextOptions<RatelDeskIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(256);
            entity.Property(user => user.IsEnabled).HasDefaultValue(true);
            entity.Property(user => user.AuthorizationRevision).HasDefaultValue(0L);
            entity.HasIndex(user => user.IsEnabled);
        });
    }
}
