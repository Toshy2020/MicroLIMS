using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class HplcRunSampleConfiguration : IEntityTypeConfiguration<HplcRunSample>
{
    public void Configure(EntityTypeBuilder<HplcRunSample> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RemovedReason).HasMaxLength(500);

        // Not unique by design - a completed run keeps its Assigned rows, so a
        // filtered unique index on (TestOrderId) where Status = Assigned is not
        // enough (see HplcRun.cs entity comment / plan Task A1). The service
        // checks "not already assigned to another open run" instead.
        builder.HasIndex(e => e.TestOrderId);

        builder.HasOne(e => e.TestOrder).WithMany().HasForeignKey(e => e.TestOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Replicates).WithOne(r => r.HplcRunSample).HasForeignKey(r => r.HplcRunSampleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcSampleReplicateConfiguration : IEntityTypeConfiguration<HplcSampleReplicate>
{
    public void Configure(EntityTypeBuilder<HplcSampleReplicate> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ActualWeightMg).HasPrecision(28, 10);

        builder.HasIndex(e => new { e.HplcRunSampleId, e.ReplicateNo }).IsUnique();

        builder.HasMany(e => e.Responses).WithOne(r => r.HplcSampleReplicate).HasForeignKey(r => r.HplcSampleReplicateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HplcReplicateResponseConfiguration : IEntityTypeConfiguration<HplcReplicateResponse>
{
    public void Configure(EntityTypeBuilder<HplcReplicateResponse> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Response).HasPrecision(28, 10);

        builder.HasIndex(e => new { e.HplcSampleReplicateId, e.HplcMethodAnalyteId }).IsUnique();
    }
}
