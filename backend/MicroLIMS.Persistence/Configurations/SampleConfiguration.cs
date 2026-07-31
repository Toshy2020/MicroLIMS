using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.ReferenceNumber).IsUnique();
        builder.Property(s => s.ReferenceNumber).IsRequired().HasMaxLength(20);
        builder.Property(s => s.ControlNumber).IsRequired().HasMaxLength(50);
        builder.Property(s => s.SampledBy).IsRequired().HasMaxLength(100);

        builder.HasOne(s => s.Item).WithMany().HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.WaterSamplingPoint).WithMany().HasForeignKey(s => s.WaterSamplingPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Department).WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Machine).WithMany().HasForeignKey(s => s.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CauseOfTesting).WithMany().HasForeignKey(s => s.CauseOfTestingId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.TestOrders)
               .WithOne(t => t.Sample)
               .HasForeignKey(t => t.SampleId);
    }
}
