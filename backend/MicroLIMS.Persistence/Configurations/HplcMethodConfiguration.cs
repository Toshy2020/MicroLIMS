using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcMethodConfiguration : IEntityTypeConfiguration<HplcMethod>
{
    public void Configure(EntityTypeBuilder<HplcMethod> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Abbreviation).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ColumnDesignation).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ColumnBrand).HasMaxLength(100);
        builder.Property(e => e.ColumnPartNumber).HasMaxLength(100);

        builder.Property(e => e.ColumnLengthMm).HasColumnType("decimal(10,3)");
        builder.Property(e => e.ColumnInternalDiameterMm).HasColumnType("decimal(10,3)");
        builder.Property(e => e.ParticleSizeUm).HasColumnType("decimal(10,3)");
        builder.Property(e => e.ColumnTemperatureC).HasColumnType("decimal(10,3)");
        builder.Property(e => e.EquilibrationMin).HasColumnType("decimal(10,3)");
        builder.Property(e => e.FlowRateMlPerMin).HasColumnType("decimal(10,3)");
        builder.Property(e => e.InjectionVolumeUl).HasColumnType("decimal(10,3)");
        builder.Property(e => e.RunTimeMin).HasColumnType("decimal(10,3)");

        builder.HasIndex(e => new { e.SectionId, e.Abbreviation }).IsUnique();

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiluentSolution).WithMany().HasForeignKey(e => e.DiluentSolutionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.MobilePhases).WithOne(c => c.HplcMethod).HasForeignKey(c => c.HplcMethodId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.GradientSteps).WithOne(c => c.HplcMethod).HasForeignKey(c => c.HplcMethodId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Analytes).WithOne(c => c.HplcMethod).HasForeignKey(c => c.HplcMethodId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcMethodMobilePhaseConfiguration : IEntityTypeConfiguration<HplcMethodMobilePhase>
{
    public void Configure(EntityTypeBuilder<HplcMethodMobilePhase> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).IsRequired().HasMaxLength(1);
        builder.Property(e => e.RatioPercent).HasColumnType("decimal(10,3)");

        builder.HasIndex(e => new { e.HplcMethodId, e.Channel }).IsUnique();

        builder.HasOne(e => e.SolutionMaster).WithMany().HasForeignKey(e => e.SolutionMasterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class HplcMethodGradientStepConfiguration : IEntityTypeConfiguration<HplcMethodGradientStep>
{
    public void Configure(EntityTypeBuilder<HplcMethodGradientStep> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TimeMin).HasColumnType("decimal(10,3)");
        builder.Property(e => e.PercentA).HasColumnType("decimal(10,3)");
        builder.Property(e => e.PercentB).HasColumnType("decimal(10,3)");
        builder.Property(e => e.PercentC).HasColumnType("decimal(10,3)");
        builder.Property(e => e.PercentD).HasColumnType("decimal(10,3)");
    }
}

public class HplcMethodAnalyteConfiguration : IEntityTypeConfiguration<HplcMethodAnalyte>
{
    public void Configure(EntityTypeBuilder<HplcMethodAnalyte> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.WavelengthNm).HasColumnType("decimal(10,3)");
        builder.Property(e => e.TheoreticalWeightStdMg).HasColumnType("decimal(12,4)");
        builder.Property(e => e.TheoreticalWeightTestMg).HasColumnType("decimal(12,4)");
        builder.Property(e => e.SstMaxRsdPercent).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMinResolution).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMaxTailingFactor).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMinTheoreticalPlates).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMinRetentionFactor).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMinSignalToNoise).HasColumnType("decimal(10,3)");
        builder.Property(e => e.SstMinPeakToValley).HasColumnType("decimal(10,3)");
        builder.Property(e => e.StandardDilution).HasColumnType("decimal(12,4)");

        builder.HasIndex(e => new { e.HplcMethodId, e.Name }).IsUnique();

        builder.HasOne(e => e.StandardEntry).WithMany().HasForeignKey(e => e.StandardEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
