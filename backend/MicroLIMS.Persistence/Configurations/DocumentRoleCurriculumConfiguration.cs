using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentRoleCurriculumConfiguration : IEntityTypeConfiguration<DocumentRoleCurriculum>
{
    public void Configure(EntityTypeBuilder<DocumentRoleCurriculum> builder)
    {
        builder.ToTable("DocumentRoleCurricula");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.HasOne(c => c.Role)
            .WithMany()
            .HasForeignKey(c => c.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Department)
            .WithMany()
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.DocumentRoleCurriculum)
            .HasForeignKey(i => i.DocumentRoleCurriculumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.RoleId);
        builder.HasIndex(c => c.DepartmentId);
        builder.HasIndex(c => c.IsActive);
    }
}
