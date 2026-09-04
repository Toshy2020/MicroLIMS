using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class PeriodicReviewTaskConfiguration : IEntityTypeConfiguration<PeriodicReviewTask>
{
    public void Configure(EntityTypeBuilder<PeriodicReviewTask> builder)
    {
        builder.ToTable("PeriodicReviewTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.ReviewSummary)
            .HasMaxLength(4000);

        builder.HasOne(t => t.DocumentMaster)
            .WithMany()
            .HasForeignKey(t => t.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DocumentRevision)
            .WithMany()
            .HasForeignKey(t => t.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedReviewerUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CompletedByUser)
            .WithMany()
            .HasForeignKey(t => t.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Findings)
            .WithOne(f => f.PeriodicReviewTask)
            .HasForeignKey(f => f.PeriodicReviewTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.DocumentMasterId);
        builder.HasIndex(t => t.DocumentRevisionId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.ScheduledDueDate);
        builder.HasIndex(t => t.AssignedReviewerUserId);
    }
}
