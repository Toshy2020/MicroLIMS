using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialCategoryConfiguration : IEntityTypeConfiguration<MaterialCategory>
{
    public void Configure(EntityTypeBuilder<MaterialCategory> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.CodePrefix).IsRequired().HasMaxLength(5);
        builder.Property(c => c.DefaultStorageCondition).HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.CodePrefix).IsUnique();
        builder.HasMany(c => c.Sections).WithOne(s => s.MaterialCategory).HasForeignKey(s => s.MaterialCategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MaterialCategorySectionConfiguration : IEntityTypeConfiguration<MaterialCategorySection>
{
    public void Configure(EntityTypeBuilder<MaterialCategorySection> builder)
    {
        builder.HasKey(s => new { s.MaterialCategoryId, s.SectionId });
        builder.HasOne(s => s.Section).WithMany().HasForeignKey(s => s.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
