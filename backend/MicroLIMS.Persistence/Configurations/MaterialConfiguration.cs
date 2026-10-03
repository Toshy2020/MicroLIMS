using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MaterialName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.CustomType).HasMaxLength(100);
        builder.Property(m => m.ManufacturerName).HasMaxLength(150);
        builder.Property(m => m.BatchNumber).HasMaxLength(100);
        builder.Property(m => m.Code).HasMaxLength(50);
        builder.Property(m => m.Location).HasMaxLength(150);
        builder.Property(m => m.AtccNumber).HasMaxLength(50);
        builder.Property(m => m.QuantityReceived).HasColumnType("decimal(18,6)");
        builder.Property(m => m.QuantityRemaining).HasColumnType("decimal(18,6)");
        builder.Property(m => m.MinimumStockLevel).HasColumnType("decimal(18,3)");
        builder.Property(m => m.Purity).HasColumnType("decimal(6,3)");
        builder.Property(m => m.MoisturePercent).HasColumnType("decimal(6,3)");
        builder.HasOne(m => m.MaterialMasterEntry).WithMany().HasForeignKey(m => m.MaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.MaterialMasterEntryId);

        // Not a unique constraint - the same material/code is legitimately
        // received again under a new batch/lot, exactly like the source list.
        builder.HasIndex(m => m.Code);
        builder.HasIndex(m => m.MaterialType);
        builder.HasIndex(m => m.MediaProductId);
        // WS-nn/MM/yyyy codes are generated (WorkingStandardService.ApproveAsync);
        // other types reuse master-entry codes, so the uniqueness is WS-only.
        builder.HasIndex(m => m.Code, "IX_Materials_WorkingStandardCode").IsUnique().HasFilter("\"MaterialType\" = 12");
        builder.HasOne(m => m.Organism).WithMany().HasForeignKey(m => m.OrganismId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.MediaProduct).WithMany().HasForeignKey(m => m.MediaProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Section)
            .WithMany()
            .HasForeignKey(m => m.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.SectionId);

        builder.Ignore(m => m.Status);
        builder.Ignore(m => m.IsUsable);
        builder.Ignore(m => m.LotLabel);
    }
}
