using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkloadWeightConfiguration : IEntityTypeConfiguration<WorkloadWeight>
{
    public void Configure(EntityTypeBuilder<WorkloadWeight> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.TestCode).HasMaxLength(50).IsRequired();
        builder.Property(w => w.TestName).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Weight).HasPrecision(5, 2);
        builder.Property(w => w.ReasonForChange).HasMaxLength(500);
        builder.Property(w => w.ChangedByName).HasMaxLength(100);

        builder.HasIndex(w => w.TestCode).IsUnique();
    }
}
