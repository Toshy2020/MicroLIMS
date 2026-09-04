using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentSectionConfiguration : IEntityTypeConfiguration<DocumentSection>
{
    public void Configure(EntityTypeBuilder<DocumentSection> builder)
    {
        builder.ToTable("DocumentSections");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(s => s.Department)
            .WithMany(d => d.Sections)
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.DepartmentId, s.Name })
            .IsUnique();
    }
}
