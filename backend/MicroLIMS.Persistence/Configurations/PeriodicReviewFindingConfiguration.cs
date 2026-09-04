using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class PeriodicReviewFindingConfiguration : IEntityTypeConfiguration<PeriodicReviewFinding>
{
    public void Configure(EntityTypeBuilder<PeriodicReviewFinding> builder)
    {
        builder.ToTable("PeriodicReviewFindings");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.SectionNumber)
            .HasMaxLength(100);

        builder.Property(f => f.NoteText)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(f => f.PeriodicReviewTask)
            .WithMany(t => t.Findings)
            .HasForeignKey(f => f.PeriodicReviewTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.CreatedByUser)
            .WithMany()
            .HasForeignKey(f => f.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.PeriodicReviewTaskId);
        builder.HasIndex(f => f.Status);
    }
}
