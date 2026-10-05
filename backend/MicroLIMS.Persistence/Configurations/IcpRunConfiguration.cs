using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IcpRunConfiguration : IEntityTypeConfiguration<IcpRun>
{
    public void Configure(EntityTypeBuilder<IcpRun> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.MethodSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.CloseReason).HasMaxLength(500);

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.EquipmentId, e.Status });

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Equipment).WithMany().HasForeignKey(e => e.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.IcpMethod).WithMany().HasForeignKey(e => e.IcpMethodId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Calibration).WithOne(c => c.IcpRun).HasForeignKey<IcpCalibration>(c => c.IcpRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.CcvReadings).WithOne(c => c.IcpRun).HasForeignKey(c => c.IcpRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Samples).WithOne(s => s.IcpRun).HasForeignKey(s => s.IcpRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Evidence).WithOne(v => v.IcpRun).HasForeignKey(v => v.IcpRunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IcpCalibrationConfiguration : IEntityTypeConfiguration<IcpCalibration>
{
    public void Configure(EntityTypeBuilder<IcpCalibration> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.IcpRunId).IsUnique();

        builder.HasOne(e => e.CalibrationStandardMaterial).WithMany().HasForeignKey(e => e.CalibrationStandardMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.IcvStandardMaterial).WithMany().HasForeignKey(e => e.IcvStandardMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Signature).WithMany().HasForeignKey(e => e.SignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Elements).WithOne(a => a.IcpCalibration).HasForeignKey(a => a.IcpCalibrationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IcpCalibrationElementConfiguration : IEntityTypeConfiguration<IcpCalibrationElement>
{
    public void Configure(EntityTypeBuilder<IcpCalibrationElement> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
        builder.Property(e => e.FailureReasons).HasMaxLength(2000);
        builder.Property(e => e.CorrelationR).HasPrecision(8, 6);
        builder.Property(e => e.BlankMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.IcvMeasuredMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.IcvRecoveryPercent).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.IcpCalibrationId, e.IcpMethodElementId }).IsUnique();
    }
}

public class IcpCcvReadingConfiguration : IEntityTypeConfiguration<IcpCcvReading>
{
    public void Configure(EntityTypeBuilder<IcpCcvReading> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
        builder.Property(e => e.MeasuredMgPerL).HasPrecision(18, 6);
        builder.Property(e => e.RecoveryPercent).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.IcpRunId, e.IcpMethodElementId });
    }
}

public class IcpRunSampleConfiguration : IEntityTypeConfiguration<IcpRunSample>
{
    public void Configure(EntityTypeBuilder<IcpRunSample> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RemovedReason).HasMaxLength(500);
        builder.Property(e => e.UnitAmount).HasPrecision(18, 6);

        // Not unique by design - a completed run keeps its Assigned rows; the service
        // checks "not already assigned to another open run".
        builder.HasIndex(e => e.TestOrderId);

        builder.HasOne(e => e.TestOrder).WithMany().HasForeignKey(e => e.TestOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Replicates).WithOne(r => r.IcpRunSample).HasForeignKey(r => r.IcpRunSampleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IcpSampleReplicateConfiguration : IEntityTypeConfiguration<IcpSampleReplicate>
{
    public void Configure(EntityTypeBuilder<IcpSampleReplicate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SampleAmount).HasPrecision(18, 6);
        builder.Property(e => e.VolumeMl).HasPrecision(18, 6);
        builder.Property(e => e.DilutionFactor).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.IcpRunSampleId, e.ReplicateNo }).IsUnique();

        builder.HasMany(e => e.Concentrations).WithOne(c => c.IcpSampleReplicate).HasForeignKey(c => c.IcpSampleReplicateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IcpReplicateConcentrationConfiguration : IEntityTypeConfiguration<IcpReplicateConcentration>
{
    public void Configure(EntityTypeBuilder<IcpReplicateConcentration> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SolutionMgPerL).HasPrecision(18, 6);

        builder.HasIndex(e => new { e.IcpSampleReplicateId, e.IcpMethodElementId }).IsUnique();
    }
}

public class IcpEvidenceConfiguration : IEntityTypeConfiguration<IcpEvidence>
{
    public void Configure(EntityTypeBuilder<IcpEvidence> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.SupersedeReason).HasMaxLength(500);

        builder.HasIndex(e => new { e.IcpRunId, e.Context, e.Kind });

        builder.HasOne(e => e.IcpRunSample).WithMany().HasForeignKey(e => e.IcpRunSampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SupersededByEvidence).WithMany().HasForeignKey(e => e.SupersededByEvidenceId).OnDelete(DeleteBehavior.Restrict);
    }
}
