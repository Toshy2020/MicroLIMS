using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AccessReviewConfiguration : IEntityTypeConfiguration<AccessReview>
{
    public void Configure(EntityTypeBuilder<AccessReview> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.PeriodLabel).HasMaxLength(100).IsRequired();
        builder.Property(r => r.SnapshotJson).IsRequired();
        builder.Property(r => r.SnapshotSha256).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Conclusion).HasMaxLength(2000);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ElectronicSignature).WithMany()
            .HasForeignKey(r => r.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.GeneratedAt);
    }
}

public class AccessReviewFindingConfiguration : IEntityTypeConfiguration<AccessReviewFinding>
{
    public void Configure(EntityTypeBuilder<AccessReviewFinding> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Kind).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Detail).HasMaxLength(1000).IsRequired();
        builder.Property(f => f.Note).HasMaxLength(1000);

        builder.HasOne(f => f.Review).WithMany(r => r.Findings)
            .HasForeignKey(f => f.ReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.ReviewId);
    }
}
