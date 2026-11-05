using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestDefinitionIcpElementConfiguration : IEntityTypeConfiguration<TestDefinitionIcpElement>
{
    public void Configure(EntityTypeBuilder<TestDefinitionIcpElement> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
        builder.Property(e => e.TargetPpm).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.TestDefinitionId, e.Symbol }).IsUnique();
    }
}
