using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CountTestReadingConfiguration : IEntityTypeConfiguration<CountTestReading>
{
    public void Configure(EntityTypeBuilder<CountTestReading> builder)
    {
        builder.HasKey(r => r.Id);

        // Stored as the member name, the same text the column held when the
        // status was a string.
        builder.Property(r => r.Status).HasConversion<string>();

        builder.Property(r => r.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(r => new { r.TestOrderId, r.StepName, r.IsActive });
    }
}
