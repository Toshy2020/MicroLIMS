using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DiscussionPostConfiguration : IEntityTypeConfiguration<DiscussionPost>
{
    public void Configure(EntityTypeBuilder<DiscussionPost> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Content).IsRequired();

        builder.HasOne(p => p.AuthorUser)
            .WithMany()
            .HasForeignKey(p => p.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.LastEditedByUser)
            .WithMany()
            .HasForeignKey(p => p.LastEditedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Category);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => p.IsImportant);
        builder.HasIndex(p => p.IsDeleted);
    }
}
