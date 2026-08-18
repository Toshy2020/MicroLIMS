using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialDocumentConfiguration : IEntityTypeConfiguration<MaterialDocument>
{
    public void Configure(EntityTypeBuilder<MaterialDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(d => d.FileExtension).IsRequired().HasMaxLength(10);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ContentSha256).IsRequired().HasMaxLength(64);
        builder.Property(d => d.SupersessionReason).HasMaxLength(1000);
        builder.Property(d => d.VoidReason).HasMaxLength(1000);

        // FK to Material - Restrict so historical documents are never
        // cascade-deleted when a material row is deleted.
        builder.HasOne(d => d.Material)
            .WithMany()
            .HasForeignKey(d => d.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to uploader user.
        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-reference for supersession chain.
        // When document A is superseded by document B, A.SupersededByDocumentId = B.Id.
        builder.HasOne(d => d.SupersededByDocument)
            .WithMany()
            .HasForeignKey(d => d.SupersededByDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes per approved spec.
        builder.HasIndex(d => d.MaterialId);
        builder.HasIndex(d => new { d.MaterialId, d.Status });
        builder.HasIndex(d => new { d.MaterialId, d.DocumentType, d.Status });
    }
}
