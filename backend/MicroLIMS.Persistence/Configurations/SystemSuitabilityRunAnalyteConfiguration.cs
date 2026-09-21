using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SystemSuitabilityRunAnalyteConfiguration : IEntityTypeConfiguration<SystemSuitabilityRunAnalyte>
{
    public void Configure(EntityTypeBuilder<SystemSuitabilityRunAnalyte> builder)
    {
        builder.ToTable("SystemSuitabilityRunAnalytes");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AnalyteName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.WavelengthNm).HasPrecision(10, 4);

        builder.Property(a => a.StandardPurityPercent).HasPrecision(28, 10);
        builder.Property(a => a.StandardWeightMg).HasPrecision(18, 6);
        builder.Property(a => a.StandardDilution).HasPrecision(18, 6);
        builder.Property(a => a.StandardMeanArea).HasPrecision(28, 10);

        builder.Property(a => a.RsdPercent).HasPrecision(28, 10);
        builder.Property(a => a.Resolution).HasPrecision(28, 10);
        builder.Property(a => a.TailingFactor).HasPrecision(28, 10);
        builder.Property(a => a.TheoreticalPlates).HasPrecision(28, 10);

        builder.Property(a => a.FailureReasons).HasMaxLength(2000);

        builder.HasOne(a => a.SystemSuitabilityRun)
            .WithMany(r => r.Analytes)
            .HasForeignKey(a => a.SystemSuitabilityRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.TestAnalyte)
            .WithMany()
            .HasForeignKey(a => a.TestAnalyteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ReferenceStandardMaterial)
            .WithMany()
            .HasForeignKey(a => a.ReferenceStandardMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.SystemSuitabilityRunId);
        builder.HasIndex(a => a.TestAnalyteId);
        builder.HasIndex(a => a.ReferenceStandardMaterialId);
        builder.HasIndex(a => new { a.SystemSuitabilityRunId, a.TestAnalyteId }).IsUnique();
    }
}
