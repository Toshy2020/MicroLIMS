using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IcpMethodConfiguration : IEntityTypeConfiguration<IcpMethod>
{
    public void Configure(EntityTypeBuilder<IcpMethod> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Abbreviation).IsRequired().HasMaxLength(20);
        builder.Property(e => e.StandardLevelsMgPerL).IsRequired().HasMaxLength(200);

        builder.Property(e => e.MinCorrelation).HasPrecision(8, 6);
        builder.Property(e => e.BlankMaxMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.IcvNominalMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.IcvRecoveryLowPercent).HasPrecision(18, 6);
        builder.Property(e => e.IcvRecoveryHighPercent).HasPrecision(18, 6);
        builder.Property(e => e.CcvNominalMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.CcvRecoveryLowPercent).HasPrecision(18, 6);
        builder.Property(e => e.CcvRecoveryHighPercent).HasPrecision(18, 6);
        builder.Property(e => e.SampleVolumeMl).HasPrecision(18, 6);
        builder.Property(e => e.DilutionFactor).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.SectionId, e.Abbreviation }).IsUnique();

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CalibrationStandardEntry).WithMany().HasForeignKey(e => e.CalibrationStandardEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.IcvStandardEntry).WithMany().HasForeignKey(e => e.IcvStandardEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Elements).WithOne(c => c.IcpMethod).HasForeignKey(c => c.IcpMethodId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IcpMethodElementConfiguration : IEntityTypeConfiguration<IcpMethodElement>
{
    public void Configure(EntityTypeBuilder<IcpMethodElement> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Symbol).IsRequired().HasMaxLength(3);
        builder.Property(e => e.WavelengthNm).HasPrecision(18, 6);
        builder.Property(e => e.ConversionFactor).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.IcpMethodId, e.DisplayOrder });
    }
}
