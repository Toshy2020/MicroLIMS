using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CalibrationRunDocumentConfiguration : IEntityTypeConfiguration<CalibrationRunDocument>
{
    public void Configure(EntityTypeBuilder<CalibrationRunDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ContentSha256).IsRequired().HasMaxLength(64);

        builder.HasIndex(d => d.CalibrationRunId).IsUnique();

        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
