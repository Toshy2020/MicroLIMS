using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DiscussionAttachmentConfiguration : IEntityTypeConfiguration<DiscussionAttachment>
{
    public void Configure(EntityTypeBuilder<DiscussionAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FileExtension).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ContentSha256).HasMaxLength(64).IsRequired();

        builder.HasOne(a => a.Post)
            .WithMany(p => p.Attachments)
            .HasForeignKey(a => a.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedByUser)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PostId);
    }
}
