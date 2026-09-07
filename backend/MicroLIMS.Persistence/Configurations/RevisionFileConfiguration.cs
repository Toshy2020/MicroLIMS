using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RevisionFileConfiguration : IEntityTypeConfiguration<RevisionFile>
{
    public void Configure(EntityTypeBuilder<RevisionFile> builder)
    {
        builder.ToTable("RevisionFiles", t =>
        {
            t.HasCheckConstraint(
                "CK_RevisionFiles_ApprovedFinalSourceRole",
                "(\"IsApprovedFinalSource\" = false) OR (\"IsApprovedFinalSource\" = true AND \"FileRole\" = 2)");

            t.HasCheckConstraint(
                "CK_RevisionFiles_GeneratedFromSourceRole",
                "(\"GeneratedFromSourceFileId\" IS NULL) OR (\"FileRole\" = 1)");

            t.HasCheckConstraint(
                "CK_RevisionFiles_NotSelfGenerated",
                "(\"GeneratedFromSourceFileId\" IS NULL) OR (\"GeneratedFromSourceFileId\" <> \"Id\")");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.ContentSha256)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(f => f.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.FileVersion)
            .HasDefaultValue(1);

        builder.Property(f => f.IsApprovedFinalSource)
            .HasDefaultValue(false);

        builder.HasOne(f => f.DocumentRevision)
            .WithMany(r => r.Files)
            .HasForeignKey(f => f.DocumentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.UploadedByUser)
            .WithMany()
            .HasForeignKey(f => f.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.SupersededByFile)
            .WithMany()
            .HasForeignKey(f => f.SupersededByFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.GeneratedFromSourceFile)
            .WithMany()
            .HasForeignKey(f => f.GeneratedFromSourceFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.DocumentRevisionId, f.FileRole })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        builder.HasIndex(f => new { f.DocumentRevisionId, f.FileRole, f.FileVersion })
            .IsUnique();

        builder.HasIndex(f => new { f.DocumentRevisionId, f.IsApprovedFinalSource })
            .IsUnique()
            .HasFilter("\"IsApprovedFinalSource\" = true");

        builder.HasIndex(f => f.GeneratedFromSourceFileId);
    }
}
