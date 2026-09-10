using Helpdesk.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence;

public class NotificationEntityConfiguration : IEntityTypeConfiguration<NotificationEntity>
{
    public void Configure(EntityTypeBuilder<NotificationEntity> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedUtc).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(256);
        builder.Property(x => x.Category).HasMaxLength(256);
        builder.Property(x => x.TenantId).HasMaxLength(256);
        builder.Property(x => x.Reference).HasMaxLength(256);
        builder.Property(x => x.CorrelationId).HasMaxLength(256);
        builder.Property(x => x.Link).HasMaxLength(1024);
        builder.Property(x => x.IsGlobal).IsRequired();

        builder.HasIndex(x => x.CreatedUtc);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.IsGlobal);
        builder.HasIndex(x => x.Reference);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.Category, x.CreatedUtc });
        builder.HasIndex(x => new { x.TenantId, x.Category });
        builder.HasIndex(x => x.CorrelationId);

        builder.HasMany(x => x.Reads)
            .WithOne(x => x.Notification)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
