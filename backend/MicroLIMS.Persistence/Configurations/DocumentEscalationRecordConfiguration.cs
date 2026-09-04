using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentEscalationRecordConfiguration : IEntityTypeConfiguration<DocumentEscalationRecord>
{
    public void Configure(EntityTypeBuilder<DocumentEscalationRecord> builder)
    {
        builder.ToTable("DocumentEscalationRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.RecipientRoleOrTarget)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EscalationReason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.ResolutionReason)
            .HasMaxLength(1000);

        builder.Property(e => e.ProcessName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(e => e.DocumentTrainingAssignment)
            .WithMany()
            .HasForeignKey(e => e.DocumentTrainingAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DocumentMaster)
            .WithMany()
            .HasForeignKey(e => e.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DocumentRevision)
            .WithMany()
            .HasForeignKey(e => e.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AssignedUser)
            .WithMany()
            .HasForeignKey(e => e.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResolvedByUser)
            .WithMany()
            .HasForeignKey(e => e.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookup indexes
        builder.HasIndex(e => e.DocumentTrainingAssignmentId);
        builder.HasIndex(e => e.DocumentMasterId);
        builder.HasIndex(e => e.DocumentRevisionId);
        builder.HasIndex(e => e.AssignedUserId);
        builder.HasIndex(e => e.EscalationLevel);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ExecutedAtUtc);

        // Strict Idempotency / Deduplication Guard:
        // An assignment can only have ONE escalation record per EscalationLevel
        builder.HasIndex(e => new { e.DocumentTrainingAssignmentId, e.EscalationLevel })
            .IsUnique();
    }
}
