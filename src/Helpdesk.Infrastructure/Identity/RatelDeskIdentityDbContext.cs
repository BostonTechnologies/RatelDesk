using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Identity;

public sealed class RatelDeskIdentityDbContext(DbContextOptions<RatelDeskIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(256);
            entity.Property(user => user.IsEnabled).HasDefaultValue(true);
            entity.Property(user => user.IsInstanceAdministrator).HasDefaultValue(false);
            entity.Property(user => user.AuthorizationRevision).HasDefaultValue(0L);
            entity.HasIndex(user => user.IsEnabled);
        });

        builder.Entity<IntegrationCredential>(entity =>
        {
            entity.ToTable("IntegrationCredentials");
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.Property(credential => credential.Name).HasMaxLength(128).IsRequired();
            entity.Property(credential => credential.Prefix).HasMaxLength(32).IsRequired();
            entity.Property(credential => credential.SecretHash).HasMaxLength(128).IsRequired();
            entity.Property(credential => credential.Purpose).HasMaxLength(16).IsRequired();
            entity.Property(credential => credential.McpResourceUri).HasMaxLength(2048);
            entity.Property(credential => credential.OrganizationId).HasMaxLength(128);
            entity.Property(credential => credential.Permissions).HasMaxLength(4096).IsRequired();
            entity.HasIndex(credential => credential.OwnerUserId);
            entity.HasIndex(credential => new { credential.OwnerUserId, credential.CreatedAtUnixMilliseconds, credential.Id });
            entity.HasIndex(credential => new { credential.Purpose, credential.ExpiresAtUtc, credential.RevokedAtUtc });
        });
    }
}
