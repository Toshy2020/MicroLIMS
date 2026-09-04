using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentMasterConfiguration : IEntityTypeConfiguration<DocumentMaster>
{
    public void Configure(EntityTypeBuilder<DocumentMaster> builder)
    {
        builder.ToTable("DocumentMasters");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MicroLimsDocumentId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.CompanyDocumentCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Category)
            .HasMaxLength(100);

        builder.Property(m => m.VoidReason)
            .HasMaxLength(2000);

        builder.HasOne(m => m.DocumentType)
            .WithMany()
            .HasForeignKey(m => m.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Department)
            .WithMany()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Section)
            .WithMany()
            .HasForeignKey(m => m.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.DocumentOwnerUser)
            .WithMany()
            .HasForeignKey(m => m.DocumentOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CreatedByUser)
            .WithMany()
            .HasForeignKey(m => m.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ModifiedByUser)
            .WithMany()
            .HasForeignKey(m => m.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.VoidedByUser)
            .WithMany()
            .HasForeignKey(m => m.VoidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CurrentEffectiveRevision)
            .WithMany()
            .HasForeignKey(m => m.CurrentEffectiveRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Revisions)
            .WithOne(r => r.DocumentMaster)
            .HasForeignKey(r => r.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Assignments)
            .WithOne(a => a.DocumentMaster)
            .HasForeignKey(a => a.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Keywords)
            .WithOne(k => k.DocumentMaster)
            .HasForeignKey(k => k.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.MicroLimsDocumentId)
            .IsUnique();

        builder.HasIndex(m => m.CompanyDocumentCode)
            .IsUnique()
            .HasFilter("\"RecordStatus\" = 1");

        builder.HasIndex(m => m.DocumentTypeId);
        builder.HasIndex(m => m.DepartmentId);
        builder.HasIndex(m => m.SectionId);
        builder.HasIndex(m => m.DocumentOwnerUserId);
    }
}
