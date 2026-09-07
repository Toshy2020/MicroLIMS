using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentRevisionConfiguration : IEntityTypeConfiguration<DocumentRevision>
{
    public void Configure(EntityTypeBuilder<DocumentRevision> builder)
    {
        builder.ToTable("DocumentRevisions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RevisionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.ReasonForRevision)
            .HasMaxLength(2000);

        builder.Property(r => r.ChangeReference)
            .HasMaxLength(200);

        builder.Property(r => r.CancelReason)
            .HasMaxLength(2000);

        builder.HasOne(r => r.DocumentMaster)
            .WithMany(m => m.Revisions)
            .HasForeignKey(r => r.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CancelledByUser)
            .WithMany()
            .HasForeignKey(r => r.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Files)
            .WithOne(f => f.DocumentRevision)
            .HasForeignKey(f => f.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Release 1d: Pointers to approved source and controlled PDF
        builder.HasOne(r => r.ApprovedSourceFile)
            .WithMany()
            .HasForeignKey(r => r.ApprovedSourceFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ControlledPdfFile)
            .WithMany()
            .HasForeignKey(r => r.ControlledPdfFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.DocumentMasterId, r.RevisionNumber })
            .IsUnique();

        builder.HasIndex(r => r.RevisionSequence);
        builder.HasIndex(r => r.ApprovedSourceFileId);
        builder.HasIndex(r => r.ControlledPdfFileId);
    }
}
