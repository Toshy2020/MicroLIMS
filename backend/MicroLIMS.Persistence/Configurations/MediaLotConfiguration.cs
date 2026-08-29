using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

// Renamed from MediaConfiguration (its filename matched, coincidentally,
// the new MediaConfiguration entity introduced by the Media Configuration
// Migration - see MicroLIMS.Domain.Entities.MediaConfiguration). This
// class configures Media (a prepared lot), not that new entity.
public class MediaLotConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.LotNumber).IsUnique();
        builder.Property(m => m.LotNumber).IsRequired().HasMaxLength(50);
        builder.Property(m => m.ManufacturerLot).HasMaxLength(50);
        builder.Property(m => m.ManufacturerName).HasMaxLength(150);

        builder.HasOne(m => m.AutoclaveEquipment).WithMany().HasForeignKey(m => m.AutoclaveEquipmentId).OnDelete(DeleteBehavior.Restrict);

        // Never let deleting a Material cascade into deleting Media history -
        // same reasoning as the Incubation FKs (see IncubationConfiguration).
        builder.HasOne(m => m.Material).WithMany().HasForeignKey(m => m.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
