using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.Code).IsUnique();
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Code).IsRequired().HasMaxLength(50);

        builder.HasMany(i => i.Specifications)
               .WithOne(s => s.Item)
               .HasForeignKey(s => s.ItemId);

        builder.HasMany(i => i.AssignedTests)
               .WithOne(t => t.Item)
               .HasForeignKey(t => t.ItemId);
    }
}
