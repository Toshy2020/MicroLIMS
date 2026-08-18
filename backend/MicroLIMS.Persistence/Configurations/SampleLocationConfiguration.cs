using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SampleLocationConfiguration : IEntityTypeConfiguration<SampleLocation>
{
    public void Configure(EntityTypeBuilder<SampleLocation> builder)
    {
        builder.HasKey(l => l.Id);

        // Locations belong to their batch sample and TestOrder - deleting
        // either takes the location rows with it.
        builder.HasOne(l => l.Sample).WithMany(s => s.Locations).HasForeignKey(l => l.SampleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.TestOrder).WithMany().HasForeignKey(l => l.TestOrderId).OnDelete(DeleteBehavior.Cascade);

        // Never let deleting/reconfiguring a Room or MachinePart test
        // config cascade into deleting result history - same reasoning
        // as TestOrderConfiguration's Room FK.
        builder.HasOne(l => l.RoomTestConfiguration).WithMany().HasForeignKey(l => l.RoomTestConfigurationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.MachinePartConfiguration).WithMany().HasForeignKey(l => l.MachinePartConfigurationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.WaterSamplingPoint).WithMany().HasForeignKey(l => l.WaterSamplingPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.SamplingConfiguration).WithMany().HasForeignKey(l => l.SamplingConfigurationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SampleId, l.TestOrderId });

        // One result row per location per test type - no duplicates.
        builder.HasIndex(l => new { l.TestOrderId, l.RoomTestConfigurationId })
            .IsUnique()
            .HasFilter("\"RoomTestConfigurationId\" IS NOT NULL");
        builder.HasIndex(l => new { l.TestOrderId, l.MachinePartConfigurationId })
            .IsUnique()
            .HasFilter("\"MachinePartConfigurationId\" IS NOT NULL");
        builder.HasIndex(l => new { l.TestOrderId, l.WaterSamplingPointId })
            .IsUnique()
            .HasFilter("\"WaterSamplingPointId\" IS NOT NULL");
    }
}
