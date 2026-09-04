using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RevisionFileConfiguration : IEntityTypeConfiguration<RevisionFile>
{
    public void Configure(EntityTypeBuilder<RevisionFile> builder)
    {
        builder.ToTable("RevisionFiles");

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

        builder.HasIndex(f => new { f.DocumentRevisionId, f.FileRole })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");
    }
}
