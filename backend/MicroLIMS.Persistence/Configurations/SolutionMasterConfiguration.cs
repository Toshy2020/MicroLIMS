using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SolutionMasterConfiguration : IEntityTypeConfiguration<SolutionMaster>
{
    public void Configure(EntityTypeBuilder<SolutionMaster> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StorageCondition).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Instructions).IsRequired().HasMaxLength(4000);

        builder.Property(e => e.FinalVolumeMl).HasColumnType("decimal(10,3)");
        builder.Property(e => e.EquivalenceMgPerMl).HasColumnType("decimal(10,3)");
        builder.Property(e => e.NominalStrength).HasColumnType("decimal(10,5)");
        builder.Property(e => e.FactorMin).HasColumnType("decimal(8,5)");
        builder.Property(e => e.FactorMax).HasColumnType("decimal(8,5)");
        builder.Property(e => e.PhTarget).HasColumnType("decimal(4,2)");
        builder.Property(e => e.PhTolerance).HasColumnType("decimal(4,2)");
        builder.Property(e => e.MaxRsdPercent).HasColumnType("decimal(6,3)");

        builder.HasIndex(e => new { e.SectionId, e.Name }).IsUnique();

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PhAdjustingEntry).WithMany().HasForeignKey(e => e.PhAdjustingEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.StandardEntry).WithMany().HasForeignKey(e => e.StandardEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReferenceSolution).WithMany().HasForeignKey(e => e.ReferenceSolutionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Components).WithOne(c => c.SolutionMaster).HasForeignKey(c => c.SolutionMasterId).OnDelete(DeleteBehavior.Cascade);
    }
}
