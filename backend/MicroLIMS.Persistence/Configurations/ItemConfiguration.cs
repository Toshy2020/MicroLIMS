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

public class ItemPreparationConfigurationConfiguration : IEntityTypeConfiguration<ItemPreparationConfiguration>
{
    public void Configure(EntityTypeBuilder<ItemPreparationConfiguration> builder)
    {
        builder.HasKey(c => c.Id);

        // One protocol per Item.
        builder.HasIndex(c => c.ItemId).IsUnique();

        builder.Property(c => c.Unit).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Technique).IsRequired().HasMaxLength(50);

        // Restrict throughout - a config is master data referenced by
        // historical SamplePreparation snapshots.
        builder.HasOne(c => c.Item).WithMany().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DiluentType).WithMany().HasForeignKey(c => c.DiluentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DiluentMedia).WithMany().HasForeignKey(c => c.DiluentMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Neutralizer).WithMany().HasForeignKey(c => c.NeutralizerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SamplePreparationConfiguration : IEntityTypeConfiguration<SamplePreparation>
{
    public void Configure(EntityTypeBuilder<SamplePreparation> builder)
    {
        builder.HasKey(p => p.Id);

        // Keep the snapshot readable even if the source config is later
        // removed - the snapshot columns on this row stand on their own.
        builder.HasOne(p => p.SourceConfiguration)
               .WithMany()
               .HasForeignKey(p => p.SourceConfigurationId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
