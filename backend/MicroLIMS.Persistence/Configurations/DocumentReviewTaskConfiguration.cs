using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentReviewTaskConfiguration : IEntityTypeConfiguration<DocumentReviewTask>
{
    public void Configure(EntityTypeBuilder<DocumentReviewTask> builder)
    {
        builder.ToTable("DocumentReviewTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.ReviewNotes)
            .HasMaxLength(4000);

        builder.Property(t => t.SubmissionNotes)
            .HasMaxLength(2000);

        builder.HasOne(t => t.DocumentRevision)
            .WithMany(r => r.ReviewTasks)
            .HasForeignKey(t => t.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedReviewerUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedByUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DecisionByUser)
            .WithMany()
            .HasForeignKey(t => t.DecisionByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Findings)
            .WithOne(f => f.DocumentReviewTask)
            .HasForeignKey(f => f.DocumentReviewTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.DocumentRevisionId);
        builder.HasIndex(t => t.AssignedReviewerUserId);
        builder.HasIndex(t => t.Status);
    }
}
