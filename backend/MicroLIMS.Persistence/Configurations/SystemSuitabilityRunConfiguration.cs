using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SystemSuitabilityRunConfiguration : IEntityTypeConfiguration<SystemSuitabilityRun>
{
    // EF's name for the unique Code index - how SystemSuitabilityService recognises a clash on save.
    public const string CodeIndexName = "IX_SystemSuitabilityRuns_Code";

    public void Configure(EntityTypeBuilder<SystemSuitabilityRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.StandardPurityPercent).HasPrecision(6, 3);
        builder.Property(r => r.StandardWeightMg).HasPrecision(18, 4);
        builder.Property(r => r.StandardDilution).HasPrecision(18, 4);
        builder.Property(r => r.StandardMeanArea).HasPrecision(18, 4);

        builder.Property(r => r.RsdPercent).HasPrecision(18, 4);
        builder.Property(r => r.Resolution).HasPrecision(18, 4);
        builder.Property(r => r.TailingFactor).HasPrecision(18, 4);
        builder.Property(r => r.TheoreticalPlates).HasPrecision(18, 4);

        builder.Property(r => r.FailureReasons).HasMaxLength(2000);
        builder.Property(r => r.Comment).HasMaxLength(500);

        builder.HasOne(r => r.TestDefinition)
            .WithMany()
            .HasForeignKey(r => r.TestDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Section)
            .WithMany()
            .HasForeignKey(r => r.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Equipment)
            .WithMany()
            .HasForeignKey(r => r.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ChromatographyColumn)
            .WithMany()
            .HasForeignKey(r => r.ChromatographyColumnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReferenceStandardMaterial)
            .WithMany()
            .HasForeignKey(r => r.ReferenceStandardMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PerformedByUser)
            .WithMany()
            .HasForeignKey(r => r.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Signature)
            .WithMany()
            .HasForeignKey(r => r.SignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Analytes)
            .WithOne(a => a.SystemSuitabilityRun)
            .HasForeignKey(a => a.SystemSuitabilityRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.SectionId);
        builder.HasIndex(r => r.TestDefinitionId);
        builder.HasIndex(r => r.PerformedAt);
    }
}
