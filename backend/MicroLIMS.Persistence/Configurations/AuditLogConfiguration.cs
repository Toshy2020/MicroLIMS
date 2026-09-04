using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.EventUid)
            .HasMaxLength(50);

        builder.Property(a => a.ActionCode)
            .HasMaxLength(100);

        builder.Property(a => a.SystemProcessName)
            .HasMaxLength(150);

        builder.Property(a => a.SourceContext)
            .HasMaxLength(100);

        builder.Property(a => a.Reason)
            .HasMaxLength(2000);

        builder.HasMany(a => a.Changes)
            .WithOne(c => c.AuditLog)
            .HasForeignKey(c => c.AuditLogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.DocumentMasterId);
        builder.HasIndex(a => a.DocumentRevisionId);
        builder.HasIndex(a => a.EventUid);
        builder.HasIndex(a => a.CorrelationId);
    }
}
