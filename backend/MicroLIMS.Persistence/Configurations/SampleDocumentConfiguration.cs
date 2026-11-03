using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SampleDocumentConfiguration : IEntityTypeConfiguration<SampleDocument>
{
    public void Configure(EntityTypeBuilder<SampleDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(d => d.FileExtension).IsRequired().HasMaxLength(10);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ContentSha256).IsRequired().HasMaxLength(64);
        builder.Property(d => d.VoidReason).HasMaxLength(1000);

        builder.HasOne(d => d.Sample)
            .WithMany()
            .HasForeignKey(d => d.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.TestOrder)
            .WithMany()
            .HasForeignKey(d => d.TestOrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.SampleId, d.Status });
        builder.HasIndex(d => d.TestOrderId);
    }
}
