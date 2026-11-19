using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.Message).IsRequired();
        builder.Property(n => n.Severity).IsRequired();

        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.TargetType).HasMaxLength(64);
        builder.Property(n => n.DedupeKey).HasMaxLength(200);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(n => new { n.UserId, n.DedupeKey })
            .IsUnique()
            .HasFilter("\"DedupeKey\" IS NOT NULL AND \"ResolvedAt\" IS NULL")
            .HasDatabaseName("IX_NotificationLogs_UserId_DedupeKey_Open");
    }
}
