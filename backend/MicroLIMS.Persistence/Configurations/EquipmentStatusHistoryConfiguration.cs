using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EquipmentStatusHistoryConfiguration : IEntityTypeConfiguration<EquipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<EquipmentStatusHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Comment)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne(h => h.EquipmentInventory)
            .WithMany()
            .HasForeignKey(h => h.EquipmentInventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.EquipmentInventoryId);
        builder.HasIndex(h => new { h.EquipmentInventoryId, h.ChangedAt });
    }
}
