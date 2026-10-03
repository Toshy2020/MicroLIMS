using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TitrantStandardizationConfiguration : IEntityTypeConfiguration<TitrantStandardization>
{
    public void Configure(EntityTypeBuilder<TitrantStandardization> builder)
    {
        builder.Property(x => x.TemperatureC).HasPrecision(5, 2);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SettingsSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.MeanFactor).HasPrecision(28, 10);
        builder.Property(e => e.RsdPercent).HasPrecision(28, 10);
        builder.Property(e => e.FailureReasons).HasMaxLength(2000);

        builder.HasIndex(e => new { e.SolutionPreparationId, e.StandardizedAt });

        builder.HasOne(e => e.SolutionPreparation).WithMany().HasForeignKey(e => e.SolutionPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Signature).WithMany().HasForeignKey(e => e.SignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Replicates).WithOne(r => r.TitrantStandardization).HasForeignKey(r => r.TitrantStandardizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
