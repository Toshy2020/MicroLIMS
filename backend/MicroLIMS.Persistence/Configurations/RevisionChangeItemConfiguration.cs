using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RevisionChangeItemConfiguration : IEntityTypeConfiguration<RevisionChangeItem>
{
    public void Configure(EntityTypeBuilder<RevisionChangeItem> builder)
    {
        builder.ToTable("RevisionChangeItems");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.SectionNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.SectionTitle)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.DescriptionOfChange)
            .IsRequired();

        builder.Property(c => c.ChangeRationale)
            .IsRequired();

        builder.Property(c => c.ChangeCategory)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Draft")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.HasOne(c => c.DocumentRevision)
            .WithMany(r => r.ChangeItems)
            .HasForeignKey(c => c.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.OriginatingReviewFinding)
            .WithMany()
            .HasForeignKey(c => c.OriginatingReviewFindingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DocumentRevisionId);
        builder.HasIndex(c => c.OriginatingReviewFindingId);
    }
}
