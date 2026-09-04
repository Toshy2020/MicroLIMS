using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentRoleCurriculumItemConfiguration : IEntityTypeConfiguration<DocumentRoleCurriculumItem>
{
    public void Configure(EntityTypeBuilder<DocumentRoleCurriculumItem> builder)
    {
        builder.ToTable("DocumentRoleCurriculumItems");

        builder.HasKey(i => i.Id);

        builder.HasOne(i => i.DocumentRoleCurriculum)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.DocumentRoleCurriculumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.DocumentMaster)
            .WithMany()
            .HasForeignKey(i => i.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.DocumentRoleCurriculumId);
        builder.HasIndex(i => i.DocumentMasterId);

        // Unique guard: A document cannot appear twice within the same role curriculum
        builder.HasIndex(i => new { i.DocumentRoleCurriculumId, i.DocumentMasterId })
            .IsUnique();
    }
}
