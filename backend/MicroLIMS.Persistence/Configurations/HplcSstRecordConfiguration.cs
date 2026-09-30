using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcSstRecordConfiguration : IEntityTypeConfiguration<HplcSstRecord>
{
    public void Configure(EntityTypeBuilder<HplcSstRecord> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.FailureReasons).HasMaxLength(2000);

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.HplcRunId).IsUnique();

        builder.HasOne(e => e.Signature).WithMany().HasForeignKey(e => e.SignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Analytes).WithOne(a => a.HplcSstRecord).HasForeignKey(a => a.HplcSstRecordId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcSstAnalyteConfiguration : IEntityTypeConfiguration<HplcSstAnalyte>
{
    public void Configure(EntityTypeBuilder<HplcSstAnalyte> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AnalyteName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FailureReasons).HasMaxLength(2000);

        builder.Property(e => e.StandardPurityPercent).HasPrecision(28, 10);
        builder.Property(e => e.StandardMoisturePercent).HasPrecision(28, 10);
        builder.Property(e => e.StandardWeightMg).HasPrecision(28, 10);
        builder.Property(e => e.MeanResponse).HasPrecision(28, 10);
        builder.Property(e => e.ComputedRsdPercent).HasPrecision(28, 10);
        builder.Property(e => e.ReportedRsdPercent).HasPrecision(28, 10);
        builder.Property(e => e.Resolution).HasPrecision(28, 10);
        builder.Property(e => e.TailingFactor).HasPrecision(28, 10);
        builder.Property(e => e.TheoreticalPlates).HasPrecision(28, 10);
        builder.Property(e => e.RetentionFactor).HasPrecision(28, 10);
        builder.Property(e => e.SignalToNoise).HasPrecision(28, 10);
        builder.Property(e => e.PeakToValley).HasPrecision(28, 10);

        builder.HasOne(e => e.StandardMaterial).WithMany().HasForeignKey(e => e.StandardMaterialId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Injections).WithOne(i => i.HplcSstAnalyte).HasForeignKey(i => i.HplcSstAnalyteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcSstInjectionConfiguration : IEntityTypeConfiguration<HplcSstInjection>
{
    public void Configure(EntityTypeBuilder<HplcSstInjection> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Response).HasPrecision(28, 10);
    }
}
