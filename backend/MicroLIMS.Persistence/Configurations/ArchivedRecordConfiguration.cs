using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ArchivedRecordConfiguration : IEntityTypeConfiguration<ArchivedRecord>
{
    public void Configure(EntityTypeBuilder<ArchivedRecord> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.DocumentId).IsRequired().HasMaxLength(100);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1024);
        builder.Property(a => a.ContentSha256).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(200);
        builder.Property(a => a.GeneratedByNameSnapshot).IsRequired().HasMaxLength(200);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
