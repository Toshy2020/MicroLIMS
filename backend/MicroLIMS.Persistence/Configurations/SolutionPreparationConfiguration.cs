using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SolutionPreparationConfiguration : IEntityTypeConfiguration<SolutionPreparation>
{
    public void Configure(EntityTypeBuilder<SolutionPreparation> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(50);
        builder.Property(e => e.RecipeSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.FinalVolumeMl).HasColumnType("decimal(12,4)");
        builder.Property(e => e.MeasuredPh).HasColumnType("decimal(4,2)");

        builder.HasIndex(e => e.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        builder.HasIndex(e => new { e.Status, e.ExpiresAt });

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SolutionMaster).WithMany().HasForeignKey(e => e.SolutionMasterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.HplcMethod).WithMany().HasForeignKey(e => e.HplcMethodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Signature).WithMany().HasForeignKey(e => e.SignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Components).WithOne(c => c.SolutionPreparation).HasForeignKey(c => c.SolutionPreparationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.StatusHistory).WithOne(h => h.SolutionPreparation).HasForeignKey(h => h.SolutionPreparationId).OnDelete(DeleteBehavior.Cascade);
    }
}
