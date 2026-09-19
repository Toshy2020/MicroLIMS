using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestDefinitionConfiguration : IEntityTypeConfiguration<TestDefinition>
{
    public void Configure(EntityTypeBuilder<TestDefinition> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).IsRequired().HasMaxLength(100);
        builder.Property(t => t.DisplayName).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.Code).IsUnique(); // one canonical row per code - the whole point of a master list

        builder.HasOne(t => t.Section)
            .WithMany()
            .HasForeignKey(t => t.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.SectionId);

        builder.Property(t => t.MethodAbbreviation).HasMaxLength(20);
        builder.Property(t => t.SstMaxRsdPercent).HasPrecision(18, 4);
        builder.Property(t => t.SstMinResolution).HasPrecision(18, 4);
        builder.Property(t => t.SstMaxTailingFactor).HasPrecision(18, 4);
        builder.Property(t => t.SstMinTheoreticalPlates).HasPrecision(18, 4);

        builder.Property(t => t.CalMinCorrelation).HasPrecision(10, 6);
        builder.Property(t => t.CalCheckRecoveryLowPercent).HasPrecision(18, 4);
        builder.Property(t => t.CalCheckRecoveryHighPercent).HasPrecision(18, 4);
        builder.Property(t => t.CalBlankMax).HasPrecision(18, 6);
        builder.Property(t => t.CalIsRecoveryLowPercent).HasPrecision(18, 4);
        builder.Property(t => t.CalIsRecoveryHighPercent).HasPrecision(18, 4);

        builder.HasMany(t => t.Analytes)
            .WithOne(a => a.TestDefinition)
            .HasForeignKey(a => a.TestDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
