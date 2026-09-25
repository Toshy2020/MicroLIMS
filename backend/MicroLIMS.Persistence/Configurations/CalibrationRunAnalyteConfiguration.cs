using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CalibrationRunAnalyteConfiguration : IEntityTypeConfiguration<CalibrationRunAnalyte>
{
    public void Configure(EntityTypeBuilder<CalibrationRunAnalyte> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Element).IsRequired().HasMaxLength(20);
        builder.Property(a => a.WavelengthNm).HasPrecision(10, 4);

        builder.Property(a => a.CorrelationValue).HasPrecision(10, 6);
        builder.Property(a => a.LowestStandardMgPerL).HasPrecision(18, 6);
        builder.Property(a => a.HighestStandardMgPerL).HasPrecision(18, 6);
        builder.Property(a => a.FailureReasons).HasMaxLength(2000);

        builder.HasOne(a => a.CalibrationRun)
            .WithMany(r => r.Analytes)
            .HasForeignKey(a => a.CalibrationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.TestAnalyte)
            .WithMany()
            .HasForeignKey(a => a.TestAnalyteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Checks)
            .WithOne(c => c.CalibrationRunAnalyte)
            .HasForeignKey(c => c.CalibrationRunAnalyteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.CalibrationRunId);
        builder.HasIndex(a => a.TestAnalyteId);
    }
}
