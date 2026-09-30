using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TitrantStandardizationReplicateConfiguration : IEntityTypeConfiguration<TitrantStandardizationReplicate>
{
    public void Configure(EntityTypeBuilder<TitrantStandardizationReplicate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.StandardWeightMg).HasPrecision(28, 10);
        builder.Property(e => e.StandardPurityPercent).HasPrecision(28, 10);
        builder.Property(e => e.ReferenceVolumeMl).HasPrecision(28, 10);
        builder.Property(e => e.ReferenceFactor).HasPrecision(28, 10);
        builder.Property(e => e.TitrantVolumeMl).HasPrecision(28, 10);
        builder.Property(e => e.BlankMl).HasPrecision(28, 10);
        builder.Property(e => e.Factor).HasPrecision(28, 10);

        builder.HasOne(e => e.StandardMaterial).WithMany().HasForeignKey(e => e.StandardMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReferencePreparation).WithMany().HasForeignKey(e => e.ReferencePreparationId).OnDelete(DeleteBehavior.Restrict);
    }
}
