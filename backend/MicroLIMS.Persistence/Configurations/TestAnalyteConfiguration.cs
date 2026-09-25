using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestAnalyteConfiguration : IEntityTypeConfiguration<TestAnalyte>
{
    public void Configure(EntityTypeBuilder<TestAnalyte> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Element).IsRequired().HasMaxLength(100);
        builder.Property(a => a.WavelengthNm).HasPrecision(10, 4);
        builder.Property(a => a.LoqMgPerL).HasPrecision(18, 6);

        builder.Property(a => a.SstMaxRsdPercent).HasPrecision(18, 4);
        builder.Property(a => a.SstMinResolution).HasPrecision(18, 4);
        builder.Property(a => a.SstMaxTailingFactor).HasPrecision(18, 4);
        builder.Property(a => a.SstMinTheoreticalPlates).HasPrecision(18, 4);

        builder.HasIndex(a => new { a.TestDefinitionId, a.Element, a.WavelengthNm }).IsUnique();

        builder.HasOne(a => a.TestDefinition)
            .WithMany(t => t.Analytes)
            .HasForeignKey(a => a.TestDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
