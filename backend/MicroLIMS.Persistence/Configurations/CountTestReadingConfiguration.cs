using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CountTestReadingConfiguration : IEntityTypeConfiguration<CountTestReading>
{
    public void Configure(EntityTypeBuilder<CountTestReading> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(r => new { r.TestOrderId, r.StepName, r.IsActive });
    }
}
