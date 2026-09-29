using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcEvidenceConfiguration : IEntityTypeConfiguration<HplcEvidence>
{
    public void Configure(EntityTypeBuilder<HplcEvidence> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.SupersedeReason).HasMaxLength(500);

        builder.HasIndex(e => new { e.HplcRunId, e.Context, e.Kind });

        builder.HasOne(e => e.HplcRunSample).WithMany().HasForeignKey(e => e.HplcRunSampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SupersededByEvidence).WithMany().HasForeignKey(e => e.SupersededByEvidenceId).OnDelete(DeleteBehavior.Restrict);
    }
}
