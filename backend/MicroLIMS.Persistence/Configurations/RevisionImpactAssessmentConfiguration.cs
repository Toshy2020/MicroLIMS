using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RevisionImpactAssessmentConfiguration : IEntityTypeConfiguration<RevisionImpactAssessment>
{
    public void Configure(EntityTypeBuilder<RevisionImpactAssessment> builder)
    {
        builder.ToTable("RevisionImpactAssessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.CompletedAt)
            .IsRequired();

        builder.HasOne(a => a.DocumentRevision)
            .WithOne(r => r.ImpactAssessment)
            .HasForeignKey<RevisionImpactAssessment>(a => a.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CompletedByUser)
            .WithMany()
            .HasForeignKey(a => a.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.DocumentRevisionId)
            .IsUnique();
    }
}
