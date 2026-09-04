using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentTrainingAssignmentConfiguration : IEntityTypeConfiguration<DocumentTrainingAssignment>
{
    public void Configure(EntityTypeBuilder<DocumentTrainingAssignment> builder)
    {
        builder.ToTable("DocumentTrainingAssignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.StatementText)
            .HasMaxLength(2000);

        builder.Property(a => a.ClosedReason)
            .HasMaxLength(1000);

        builder.Property(a => a.AssignmentReason)
            .HasMaxLength(1000);

        builder.HasOne(a => a.DocumentMaster)
            .WithMany()
            .HasForeignKey(a => a.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.DocumentRevision)
            .WithMany()
            .HasForeignKey(a => a.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssignedUser)
            .WithMany()
            .HasForeignKey(a => a.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AcknowledgedByUser)
            .WithMany()
            .HasForeignKey(a => a.AcknowledgedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ModifiedByUser)
            .WithMany()
            .HasForeignKey(a => a.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.SourceAssignment)
            .WithMany()
            .HasForeignKey(a => a.SourceAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Concurrency & lookup indexes
        builder.HasIndex(a => a.DocumentRevisionId);
        builder.HasIndex(a => a.DocumentMasterId);
        builder.HasIndex(a => a.AssignedUserId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.DueDateUtc);
        builder.HasIndex(a => a.SourceAssignmentId);

        // Composite index for fast active/matrix queries: User + Revision + Status
        builder.HasIndex(a => new { a.AssignedUserId, a.DocumentRevisionId, a.Status });

        // Uniqueness guard: A user can only hold one assignment per document revision for a specific assignment type
        builder.HasIndex(a => new { a.DocumentRevisionId, a.AssignedUserId, a.AssignmentType })
            .IsUnique();
    }
}
