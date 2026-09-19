using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CalibrationRunCheckConfiguration : IEntityTypeConfiguration<CalibrationRunCheck>
{
    public void Configure(EntityTypeBuilder<CalibrationRunCheck> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NominalMgPerL).HasPrecision(18, 6);
        builder.Property(c => c.MeasuredMgPerL).HasPrecision(18, 6);
        builder.Property(c => c.RecoveryPercent).HasPrecision(28, 10);

        builder.HasOne(c => c.CalibrationRunAnalyte)
            .WithMany(a => a.Checks)
            .HasForeignKey(c => c.CalibrationRunAnalyteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CalibrationRunAnalyteId);
    }
}
