using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IncubatorSetPointHistoryConfiguration : IEntityTypeConfiguration<IncubatorSetPointHistory>
{
    public void Configure(EntityTypeBuilder<IncubatorSetPointHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.PreviousSetPoint).HasPrecision(5, 2);
        builder.Property(h => h.NewSetPoint).HasPrecision(5, 2);
        builder.Property(h => h.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(h => h.Equipment)
            .WithMany()
            .HasForeignKey(h => h.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
