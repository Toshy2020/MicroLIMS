using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ChromatographyColumnConfiguration : IEntityTypeConfiguration<ChromatographyColumn>
{
    public void Configure(EntityTypeBuilder<ChromatographyColumn> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.SerialNumber).HasMaxLength(100);

        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasOne(c => c.Section)
            .WithMany()
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.SectionId);

        builder.HasMany(c => c.CompatibleEquipment)
            .WithMany(e => e.CompatibleColumns);
    }
}
