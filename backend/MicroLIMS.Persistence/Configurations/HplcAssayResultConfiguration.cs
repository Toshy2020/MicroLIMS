using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcAssayResultConfiguration : IEntityTypeConfiguration<HplcAssayResult>
{
    public void Configure(EntityTypeBuilder<HplcAssayResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.StandardPurityPercent).HasPrecision(6, 3);
        builder.Property(r => r.StandardWeightMg).HasPrecision(18, 4);
        builder.Property(r => r.StandardDilution).HasPrecision(18, 4);
        builder.Property(r => r.StandardMeanArea).HasPrecision(18, 4);

        builder.Property(r => r.SampleWeightMg).HasPrecision(18, 4);
        builder.Property(r => r.SampleDilution).HasPrecision(18, 4);
        builder.Property(r => r.MeanAssayPercent).HasPrecision(18, 4);

        builder.Property(r => r.ReplicatesJson).HasColumnType("jsonb");
        builder.Property(r => r.ReportedResult).IsRequired().HasMaxLength(100);
        builder.Property(r => r.AlertLimit).HasMaxLength(100);
        builder.Property(r => r.ActionLimit).HasMaxLength(100);
        builder.Property(r => r.SpecLimit).HasMaxLength(100);
        builder.Property(r => r.ComparisonStatus).IsRequired().HasMaxLength(50);

        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.TestOrder)
            .WithMany()
            .HasForeignKey(r => r.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SystemSuitabilityRun)
            .WithMany()
            .HasForeignKey(r => r.SystemSuitabilityRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.EnteredByUser)
            .WithMany()
            .HasForeignKey(r => r.EnteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Signature)
            .WithMany()
            .HasForeignKey(r => r.SignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TestOrderId, r.IsActive });
        builder.HasIndex(r => r.SystemSuitabilityRunId);
        builder.HasIndex(r => r.EnteredAt);
    }
}
