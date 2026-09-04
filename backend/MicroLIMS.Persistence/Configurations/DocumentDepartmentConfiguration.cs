using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentDepartmentConfiguration : IEntityTypeConfiguration<DocumentDepartment>
{
    public void Configure(EntityTypeBuilder<DocumentDepartment> builder)
    {
        builder.ToTable("DocumentDepartments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(d => d.Code)
            .IsUnique();

        builder.HasMany(d => d.Sections)
            .WithOne(s => s.Department)
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
