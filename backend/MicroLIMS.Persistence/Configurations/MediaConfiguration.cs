using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.LotNumber).IsUnique();
        builder.Property(m => m.LotNumber).IsRequired().HasMaxLength(50);
        builder.Property(m => m.ManufacturerLot).HasMaxLength(50);
        builder.Property(m => m.ManufacturerName).HasMaxLength(150);

        builder.HasOne(m => m.MediaType).WithMany().HasForeignKey(m => m.MediaTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AutoclaveEquipment).WithMany().HasForeignKey(m => m.AutoclaveEquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
