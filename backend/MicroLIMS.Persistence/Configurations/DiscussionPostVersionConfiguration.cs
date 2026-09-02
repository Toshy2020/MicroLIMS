using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DiscussionPostVersionConfiguration : IEntityTypeConfiguration<DiscussionPostVersion>
{
    public void Configure(EntityTypeBuilder<DiscussionPostVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Title).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Content).IsRequired();

        builder.HasOne(v => v.Post)
            .WithMany(p => p.Versions)
            .HasForeignKey(v => v.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.ChangedByUser)
            .WithMany()
            .HasForeignKey(v => v.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.PostId, v.VersionNumber });
    }
}
