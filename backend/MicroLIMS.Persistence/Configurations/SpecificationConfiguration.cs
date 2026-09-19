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

        builder.Property(s => s.LowerLimit).HasPrecision(18, 6);
        builder.Property(s => s.UpperLimit).HasPrecision(18, 6);
        builder.Property(s => s.LowerInclusive).HasDefaultValue(true);
        builder.Property(s => s.UpperInclusive).HasDefaultValue(true);

        builder.Property(s => s.Target).HasPrecision(18, 6);
        builder.Property(s => s.Tolerance).HasPrecision(18, 6);

        builder.Property(s => s.ExpectedResultText).HasMaxLength(1000);
        builder.Property(s => s.SampleQuantity).HasPrecision(18, 6);
        builder.Property(s => s.SampleQuantityUnit).HasMaxLength(20);

        builder.HasMany(s => s.Stages)
               .WithOne(st => st.Specification)
               .HasForeignKey(st => st.SpecificationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ItemId, s.TestCode, s.ParameterName }).IsUnique();
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
