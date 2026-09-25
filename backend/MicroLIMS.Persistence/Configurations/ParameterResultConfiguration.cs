using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ParameterResultConfiguration : IEntityTypeConfiguration<ParameterResult>
{
    public void Configure(EntityTypeBuilder<ParameterResult> builder)
    {
        builder.ToTable("ParameterResults");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ParameterName).IsRequired().HasMaxLength(150);
        builder.Property(r => r.ReportedValue).HasPrecision(28, 10);
        builder.Property(r => r.ReportedDisplay).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Unit).HasMaxLength(50);
        builder.Property(r => r.SpecLimit).HasMaxLength(100);
        builder.Property(r => r.ComparisonStatus).IsRequired().HasMaxLength(50);
        builder.Property(r => r.CalculationJson).HasColumnType("jsonb");
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.TestAnalysis)
            .WithMany(a => a.ParameterResults)
            .HasForeignKey(r => r.TestAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.TestOrder)
            .WithMany()
            .HasForeignKey(r => r.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Specification)
            .WithMany()
            .HasForeignKey(r => r.SpecificationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CalibrationRunAnalyte)
            .WithMany()
            .HasForeignKey(r => r.ValidityRecordItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TestOrderId, r.IsActive });
        builder.HasIndex(r => r.TestAnalysisId);
        builder.HasIndex(r => r.SpecificationId);
        builder.HasIndex(r => r.ValidityRecordItemId);
    }
}
