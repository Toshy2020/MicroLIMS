using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EquipmentInventoryConfiguration : IEntityTypeConfiguration<EquipmentInventory>
{
    public void Configure(EntityTypeBuilder<EquipmentInventory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.Code).IsRequired().HasMaxLength(80);
        builder.Property(e => e.InstrumentType).IsRequired().HasMaxLength(150);
        builder.Property(e => e.ManufacturerName).HasMaxLength(150);
        builder.Property(e => e.SerialNumber).HasMaxLength(100);
        builder.Property(e => e.FirmwareVersion).HasMaxLength(100);
        builder.Property(e => e.Location).HasMaxLength(150);

        builder.Ignore(e => e.IsCalibrationOverdue);
    }
}
