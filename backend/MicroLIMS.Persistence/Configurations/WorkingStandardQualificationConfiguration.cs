using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkingStandardQualificationConfiguration : IEntityTypeConfiguration<WorkingStandardQualification>
{
    public void Configure(EntityTypeBuilder<WorkingStandardQualification> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.SourceMaterialName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.SourceBatchNumber).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.QuantityGrams).HasPrecision(12, 4);
        builder.Property(e => e.MoisturePercent).HasPrecision(6, 3);
        builder.Property(e => e.MeanAssayPercent).HasPrecision(9, 4);
        builder.Property(e => e.RsdPercent).HasPrecision(9, 4);
        builder.Property(e => e.PotencyPercent).HasPrecision(6, 3);
        builder.Property(e => e.FailureReasons).HasMaxLength(1000);
        builder.Property(e => e.ReplicateAssaysJson).HasColumnType("jsonb");
        builder.Property(e => e.RejectReason).HasMaxLength(500);
        builder.Property(e => e.ReturnReason).HasMaxLength(500);
        builder.Ignore(e => e.IsOpen);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_WorkingStandardQualifications_Code");
        builder.HasIndex(e => new { e.WorkingStandardMaterialId, e.Status });

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WorkingStandardMaterial).WithMany().HasForeignKey(e => e.WorkingStandardMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MaterialMasterEntry).WithMany().HasForeignKey(e => e.MaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SourceSample).WithMany().HasForeignKey(e => e.SourceSampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HplcMethodAnalyte>().WithMany().HasForeignKey(e => e.HplcMethodAnalyteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PreparedSignature).WithMany().HasForeignKey(e => e.PreparedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReviewedSignature).WithMany().HasForeignKey(e => e.ReviewedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ApprovedSignature).WithMany().HasForeignKey(e => e.ApprovedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RejectedSignature).WithMany().HasForeignKey(e => e.RejectedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Documents).WithOne(d => d.WorkingStandardQualification)
            .HasForeignKey(d => d.WorkingStandardQualificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkingStandardDocumentConfiguration : IEntityTypeConfiguration<WorkingStandardDocument>
{
    public void Configure(EntityTypeBuilder<WorkingStandardDocument> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
    }
}
