using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IncubationConfiguration : IEntityTypeConfiguration<Incubation>
{
    public void Configure(EntityTypeBuilder<Incubation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasOne(i => i.Media)
            .WithMany()
            .HasForeignKey(i => i.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.IncubatorEquipment)
            .WithMany()
            .HasForeignKey(i => i.IncubatorEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ParentIncubation)
            .WithMany()
            .HasForeignKey(i => i.ParentIncubationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Matches the C# property initializer (= 1) - without this, the
        // migration's ALTER TABLE ADD COLUMN backfills existing rows with
        // the CLR-type default (0) instead, breaking the "StageNumber 1 =
        // a normal, non-transfer window" invariant for historical data.
        builder.Property(i => i.StageNumber).HasDefaultValue(1);

        builder.Ignore(i => i.IsIncubationComplete);
    }
}
