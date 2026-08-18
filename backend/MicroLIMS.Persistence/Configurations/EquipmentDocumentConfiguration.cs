using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EquipmentDocumentConfiguration : IEntityTypeConfiguration<EquipmentDocument>
{
    public void Configure(EntityTypeBuilder<EquipmentDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(d => d.FileExtension).IsRequired().HasMaxLength(10);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ContentSha256).IsRequired().HasMaxLength(64);
        builder.Property(d => d.SupersessionReason).HasMaxLength(1000);
        builder.Property(d => d.VoidReason).HasMaxLength(1000);

        // FK to EquipmentInventory.
        builder.HasOne(d => d.EquipmentInventory)
            .WithMany()
            .HasForeignKey(d => d.EquipmentInventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to uploader user.
        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-reference for supersession chain.
        builder.HasOne(d => d.SupersededByDocument)
            .WithMany()
            .HasForeignKey(d => d.SupersededByDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.EquipmentInventoryId);
        builder.HasIndex(d => new { d.EquipmentInventoryId, d.Status });
        builder.HasIndex(d => new { d.EquipmentInventoryId, d.DocumentType, d.Status });
    }
}
