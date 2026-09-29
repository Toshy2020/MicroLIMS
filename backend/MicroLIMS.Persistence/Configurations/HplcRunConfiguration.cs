using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcRunConfiguration : IEntityTypeConfiguration<HplcRun>
{
    public void Configure(EntityTypeBuilder<HplcRun> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.MethodSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.CloseReason).HasMaxLength(500);

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.EquipmentId, e.Status });

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Equipment).WithMany().HasForeignKey(e => e.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ChromatographyColumn).WithMany().HasForeignKey(e => e.ChromatographyColumnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.HplcMethod).WithMany().HasForeignKey(e => e.HplcMethodId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.MobilePhases).WithOne(m => m.HplcRun).HasForeignKey(m => m.HplcRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Sst).WithOne(s => s.HplcRun).HasForeignKey<HplcSstRecord>(s => s.HplcRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Samples).WithOne(s => s.HplcRun).HasForeignKey(s => s.HplcRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Evidence).WithOne(v => v.HplcRun).HasForeignKey(v => v.HplcRunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcRunMobilePhaseConfiguration : IEntityTypeConfiguration<HplcRunMobilePhase>
{
    public void Configure(EntityTypeBuilder<HplcRunMobilePhase> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).IsRequired().HasMaxLength(1);

        builder.HasIndex(e => new { e.HplcRunId, e.Channel }).IsUnique();

        builder.HasOne(e => e.SolutionPreparation).WithMany().HasForeignKey(e => e.SolutionPreparationId).OnDelete(DeleteBehavior.Restrict);
    }
}
