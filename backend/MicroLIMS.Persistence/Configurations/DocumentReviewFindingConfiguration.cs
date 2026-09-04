using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentReviewFindingConfiguration : IEntityTypeConfiguration<DocumentReviewFinding>
{
    public void Configure(EntityTypeBuilder<DocumentReviewFinding> builder)
    {
        builder.ToTable("DocumentReviewFindings");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.SectionNumber)
            .HasMaxLength(100);

        builder.Property(f => f.CommentText)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(f => f.AuthorResponse)
            .HasMaxLength(4000);

        builder.Property(f => f.ReviewerVerificationNotes)
            .HasMaxLength(4000);

        builder.HasOne(f => f.DocumentReviewTask)
            .WithMany(t => t.Findings)
            .HasForeignKey(f => f.DocumentReviewTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.CreatedByUser)
            .WithMany()
            .HasForeignKey(f => f.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.AuthorResponseByUser)
            .WithMany()
            .HasForeignKey(f => f.AuthorResponseByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ReviewerVerifiedByUser)
            .WithMany()
            .HasForeignKey(f => f.ReviewerVerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ResolvedByUser)
            .WithMany()
            .HasForeignKey(f => f.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.DocumentReviewTaskId);
        builder.HasIndex(f => f.Status);
        builder.HasIndex(f => f.IsMandatory);
    }
}
