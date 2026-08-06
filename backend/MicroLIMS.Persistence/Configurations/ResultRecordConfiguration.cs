using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ResultRecordConfiguration : IEntityTypeConfiguration<ResultRecord>
{
    public void Configure(EntityTypeBuilder<ResultRecord> builder)
    {
        builder.HasKey(r => r.Id);

        // Projection rows never cascade-delete their source Sample/TestOrder
        // and must never block deleting them either in a way that loses the
        // reporting history - Restrict keeps the FK for traceability without
        // letting a Sample delete cascade into a silent projection wipe.
        builder.HasOne(r => r.Sample).WithMany().HasForeignKey(r => r.SampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.TestOrder).WithMany().HasForeignKey(r => r.TestOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.Category, r.ResultEnteredAt });
        builder.HasIndex(r => new { r.TestCode, r.ResultEnteredAt });
        builder.HasIndex(r => r.SampleId);
        builder.HasIndex(r => r.ResultLevel);
        builder.HasIndex(r => r.SampleStatus);
        builder.HasIndex(r => r.SubjectName);

        // One projection row per source row per round - re-running an
        // upsert for the same source updates in place instead of duplicating.
        builder.HasIndex(r => new { r.SourceTable, r.SourceId, r.Round }).IsUnique();
    }
}
