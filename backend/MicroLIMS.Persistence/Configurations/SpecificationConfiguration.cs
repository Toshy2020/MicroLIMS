using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SpecificationConfiguration : IEntityTypeConfiguration<Specification>
{
    public void Configure(EntityTypeBuilder<Specification> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ParameterName).IsRequired().HasMaxLength(150);
        builder.Property(s => s.DisplayOrder).HasDefaultValue(0);
        builder.Property(s => s.LimitType).IsRequired();
        builder.Property(s => s.ReferenceStandard).HasMaxLength(100);
        builder.Property(s => s.Grade).HasMaxLength(20);

        builder.Property(s => s.LowerLimit).HasPrecision(18, 6);
        builder.Property(s => s.UpperLimit).HasPrecision(18, 6);
        builder.Property(s => s.LowerInclusive).HasDefaultValue(true);
        builder.Property(s => s.UpperInclusive).HasDefaultValue(true);

        builder.Property(s => s.Target).HasPrecision(18, 6);
        builder.Property(s => s.Tolerance).HasPrecision(18, 6);

        builder.Property(s => s.ExpectedResultText).HasMaxLength(1000);
        builder.Property(s => s.SampleQuantity).HasPrecision(18, 6);
        builder.Property(s => s.SampleQuantityUnit).HasMaxLength(20);

        builder.Property(s => s.LabelClaim).HasPrecision(18, 6);
        builder.Property(s => s.LabelClaimUnit).HasMaxLength(20);
        builder.Property(s => s.DosageForm);

        builder.HasOne(s => s.HplcMethodAnalyte)
            .WithMany()
            .HasForeignKey(s => s.HplcMethodAnalyteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.HplcMethodImpurity)
            .WithMany()
            .HasForeignKey(s => s.HplcMethodImpurityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.IcpTestElement)
            .WithMany()
            .HasForeignKey(s => s.IcpTestElementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Stages)
               .WithOne(st => st.Specification)
               .HasForeignKey(st => st.SpecificationId)
               .OnDelete(DeleteBehavior.Cascade);

        // One row per parameter per production stage and grade; NULL (every
        // stage / ungraded) counts as a value of its own, so two such rows still clash.
        builder.HasIndex(s => new { s.ItemId, s.TestCode, s.ParameterName, s.ProductionStageRole, s.Grade, s.RelatedSubstanceRow, s.HplcMethodImpurityId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}

public class SpecificationStageConfiguration : IEntityTypeConfiguration<SpecificationStage>
{
    public void Configure(EntityTypeBuilder<SpecificationStage> builder)
    {
        builder.HasKey(st => st.Id);

        builder.Property(st => st.StageLabel).IsRequired().HasMaxLength(100);
        builder.Property(st => st.AcceptanceCriteriaText).IsRequired().HasMaxLength(1000);
    }
}
