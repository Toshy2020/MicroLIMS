using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AuditEventChangeConfiguration : IEntityTypeConfiguration<AuditEventChange>
{
    public void Configure(EntityTypeBuilder<AuditEventChange> builder)
    {
        builder.ToTable("AuditEventChanges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FieldName)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasOne(c => c.AuditLog)
            .WithMany(a => a.Changes)
            .HasForeignKey(c => c.AuditLogId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
