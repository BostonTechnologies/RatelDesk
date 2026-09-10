using Helpdesk.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Infrastructure.Persistence;

public class NotificationReadEntityConfiguration : IEntityTypeConfiguration<NotificationReadEntity>
{
    public void Configure(EntityTypeBuilder<NotificationReadEntity> builder)
    {
        builder.ToTable("NotificationReads");

        builder.HasKey(x => new { x.NotificationId, x.UserId });
        builder.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReadUtc).IsRequired();
        builder.HasIndex(x => x.UserId);
    }
}
