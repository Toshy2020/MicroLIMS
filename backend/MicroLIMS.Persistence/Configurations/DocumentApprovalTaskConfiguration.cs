using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentApprovalTaskConfiguration : IEntityTypeConfiguration<DocumentApprovalTask>
{
    public void Configure(EntityTypeBuilder<DocumentApprovalTask> builder)
    {
        builder.ToTable("DocumentApprovalTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.DecisionNotes)
            .HasMaxLength(4000);

        builder.Property(t => t.SubmissionNotes)
            .HasMaxLength(2000);

        builder.HasOne(t => t.DocumentRevision)
            .WithMany(r => r.ApprovalTasks)
            .HasForeignKey(t => t.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedApproverUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedByUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DecisionByUser)
            .WithMany()
            .HasForeignKey(t => t.DecisionByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Release 1d: Approval dossier linkages
        builder.HasOne(t => t.ApprovedSourceFile)
            .WithMany()
            .HasForeignKey(t => t.ApprovedSourceFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.GeneratedControlledPdf)
            .WithMany()
            .HasForeignKey(t => t.GeneratedControlledPdfId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.DocumentRevisionId);
        builder.HasIndex(t => t.AssignedApproverUserId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.ApprovedSourceFileId);
        builder.HasIndex(t => t.GeneratedControlledPdfId);
    }
}
