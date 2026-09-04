using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentAcknowledgementRecordConfiguration : IEntityTypeConfiguration<DocumentAcknowledgementRecord>
{
    public void Configure(EntityTypeBuilder<DocumentAcknowledgementRecord> builder)
    {
        builder.ToTable("DocumentAcknowledgementRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StatementText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.ControlledFileHash)
            .HasMaxLength(64);

        builder.Property(x => x.Comments)
            .HasMaxLength(1000);

        builder.Property(x => x.ClientIpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.AcknowledgedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // 1-to-1 relationship with DocumentTrainingAssignment (Unique index)
        builder.HasOne(x => x.DocumentTrainingAssignment)
            .WithOne(a => a.AcknowledgementRecord)
            .HasForeignKey<DocumentAcknowledgementRecord>(x => x.DocumentTrainingAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DocumentTrainingAssignmentId)
            .IsUnique();

        // Foreign keys to DocumentMaster, DocumentRevision, User, RevisionFile
        builder.HasOne(x => x.DocumentMaster)
            .WithMany()
            .HasForeignKey(x => x.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocumentRevision)
            .WithMany()
            .HasForeignKey(x => x.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcknowledgedByUser)
            .WithMany()
            .HasForeignKey(x => x.AcknowledgedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ControlledFile)
            .WithMany()
            .HasForeignKey(x => x.ControlledFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inspection & Audit indexes
        builder.HasIndex(x => new { x.DocumentRevisionId, x.AcknowledgedByUserId });
        builder.HasIndex(x => new { x.DocumentMasterId, x.AcknowledgedByUserId });
        builder.HasIndex(x => x.AcknowledgedAtUtc);
    }
}
