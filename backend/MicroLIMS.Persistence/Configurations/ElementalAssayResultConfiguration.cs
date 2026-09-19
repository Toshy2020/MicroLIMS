using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ElementalAssayResultConfiguration : IEntityTypeConfiguration<ElementalAssayResult>
{
    public void Configure(EntityTypeBuilder<ElementalAssayResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ParameterName).IsRequired().HasMaxLength(150);
        builder.Property(r => r.Element).IsRequired().HasMaxLength(20);

        builder.Property(r => r.ReportedPpm).HasPrecision(18, 6);
        builder.Property(r => r.MgPerUnit).HasPrecision(28, 10);
        builder.Property(r => r.ResultClaim).HasPrecision(28, 10);
        builder.Property(r => r.PercentLabelClaim).HasPrecision(28, 10);
        builder.Property(r => r.ReportedValue).HasPrecision(28, 10);

        builder.Property(r => r.ReportedDisplay).IsRequired().HasMaxLength(100);
        builder.Property(r => r.SpecLimit).HasMaxLength(100);
        builder.Property(r => r.Unit).HasMaxLength(50);
        builder.Property(r => r.ComparisonStatus).IsRequired().HasMaxLength(50);
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.Entry)
            .WithMany(e => e.Results)
            .HasForeignKey(r => r.EntryId)
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
            .HasForeignKey(r => r.CalibrationRunAnalyteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TestOrderId, r.IsActive });
        builder.HasIndex(r => r.EntryId);
        builder.HasIndex(r => r.SpecificationId);
        builder.HasIndex(r => r.CalibrationRunAnalyteId);
    }
}
