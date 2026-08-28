using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkloadWeightHistoryConfiguration : IEntityTypeConfiguration<WorkloadWeightHistory>
{
    public void Configure(EntityTypeBuilder<WorkloadWeightHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Action).HasMaxLength(50).IsRequired();
        builder.Property(h => h.TestCode).HasMaxLength(50).IsRequired();
        builder.Property(h => h.PreviousWeight).HasPrecision(5, 2);
        builder.Property(h => h.NewWeight).HasPrecision(5, 2);
        builder.Property(h => h.ReasonForChange).HasMaxLength(500);
        builder.Property(h => h.ChangedByName).HasMaxLength(100);

        builder.HasOne(h => h.WorkloadWeight)
            .WithMany(w => w.History)
            .HasForeignKey(h => h.WorkloadWeightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
