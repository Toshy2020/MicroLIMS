using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.ToTable("EmailOutbox");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.ToAddress).IsRequired().HasMaxLength(320);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(500);

        builder.HasOne(e => e.NotificationLog)
            .WithMany()
            .HasForeignKey(e => e.NotificationLogId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.NextAttemptAt)
            .HasFilter("\"SentAt\" IS NULL AND \"FailedAt\" IS NULL")
            .HasDatabaseName("IX_EmailOutbox_NextAttemptAt_Pending");

        builder.HasIndex(e => e.NotificationLogId);
        builder.HasIndex(e => e.UserId);
    }
}
