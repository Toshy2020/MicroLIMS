using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentMasterSectionConfiguration : IEntityTypeConfiguration<DocumentMasterSection>
{
    public void Configure(EntityTypeBuilder<DocumentMasterSection> builder)
    {
        builder.ToTable("DocumentMasterSections");

        builder.HasKey(s => new { s.DocumentMasterId, s.SectionId });

        builder.HasOne(s => s.DocumentMaster)
            .WithMany(m => m.Sections)
            .HasForeignKey(s => s.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.SectionId);
    }
}
