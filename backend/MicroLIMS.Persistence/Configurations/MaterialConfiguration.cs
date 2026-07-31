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
        builder.Property(m => m.ManufacturerName).HasMaxLength(150);
        builder.Property(m => m.BatchNumber).HasMaxLength(100);
        builder.Property(m => m.Code).HasMaxLength(50);
        builder.Property(m => m.Location).HasMaxLength(150);
        builder.Property(m => m.QuantityReceived).HasColumnType("decimal(18,3)");
        builder.Property(m => m.QuantityRemaining).HasColumnType("decimal(18,3)");
        builder.Property(m => m.MinimumStockLevel).HasColumnType("decimal(18,3)");

        // Not a unique constraint - the same material/code is legitimately
        // received again under a new batch/lot, exactly like the source list.
        builder.HasIndex(m => m.Code);
        builder.HasIndex(m => m.MaterialType);

        builder.Ignore(m => m.Status);
        builder.Ignore(m => m.IsUsable);
    }
}
