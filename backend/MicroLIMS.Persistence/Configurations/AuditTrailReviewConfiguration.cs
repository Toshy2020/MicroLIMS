using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AuditTrailReviewConfiguration : IEntityTypeConfiguration<AuditTrailReview>
{
    public void Configure(EntityTypeBuilder<AuditTrailReview> builder)
    {
        builder.ToTable("AuditTrailReviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ScopeEntityName).HasMaxLength(150);
        builder.Property(r => r.ScopeRecordId).HasMaxLength(100);
        builder.Property(r => r.Conclusion).HasMaxLength(2000);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ElectronicSignature).WithMany()
            .HasForeignKey(r => r.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.StartedAtUtc);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.StartedByUserId);
        builder.HasIndex(r => r.ElectronicSignatureId);
    }
}

public class AuditTrailReviewFindingConfiguration : IEntityTypeConfiguration<AuditTrailReviewFinding>
{
    public void Configure(EntityTypeBuilder<AuditTrailReviewFinding> builder)
    {
        builder.ToTable("AuditTrailReviewFindings");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Note).HasMaxLength(2000).IsRequired();
        builder.Property(f => f.ClosedComment).HasMaxLength(2000);

        builder.HasOne(f => f.Review).WithMany(r => r.Findings)
            .HasForeignKey(f => f.ReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AuditLog>().WithMany()
            .HasForeignKey(f => f.AuditLogId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.ClosedByUserId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.ReviewId, f.AuditLogId }).IsUnique();
        builder.HasIndex(f => f.AuditLogId);
        builder.HasIndex(f => f.RecordedByUserId);
        builder.HasIndex(f => f.ClosedByUserId);
        builder.HasIndex(f => f.Status);
    }
}
