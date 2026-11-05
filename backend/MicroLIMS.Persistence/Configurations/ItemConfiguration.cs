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
        builder.Property(i => i.MinSampleQuantity).HasPrecision(18, 6);
        builder.Property(i => i.MinRetestSampleQuantity).HasPrecision(18, 6);
        builder.Property(i => i.MinSampleQuantityUnit).HasMaxLength(20);

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

        builder.Property(c => c.Technique).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Diluent).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Neutralizer).IsRequired().HasMaxLength(200);

        // Restrict - a config is master data referenced by historical
        // SamplePreparation snapshots.
        builder.HasOne(c => c.Item).WithMany().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Restrict);

        // The masters the diluent / neutralizer point at. Restrict for the
        // same reason: a master in use by a protocol is deactivated, not deleted.
        builder.HasOne(c => c.DiluentMediaProduct).WithMany().HasForeignKey(c => c.DiluentMediaProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DiluentSolutionMaster).WithMany().HasForeignKey(c => c.DiluentSolutionMasterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DiluentMaterialMasterEntry).WithMany().HasForeignKey(c => c.DiluentMaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.NeutralizerMediaProduct).WithMany().HasForeignKey(c => c.NeutralizerMediaProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.NeutralizerSolutionMaster).WithMany().HasForeignKey(c => c.NeutralizerSolutionMasterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.NeutralizerMaterialMasterEntry).WithMany().HasForeignKey(c => c.NeutralizerMaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SamplePreparationConfiguration : IEntityTypeConfiguration<SamplePreparation>
{
    public void Configure(EntityTypeBuilder<SamplePreparation> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Technique).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Diluent).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Neutralizer).IsRequired().HasMaxLength(200);

        // Keep the snapshot readable even if the source config is later
        // removed - the snapshot columns on this row stand on their own.
        builder.HasOne(p => p.SourceConfiguration)
               .WithMany()
               .HasForeignKey(p => p.SourceConfigurationId)
               .OnDelete(DeleteBehavior.Restrict);

        // The lots picked at confirmation: Restrict, so a lot that was used
        // can never be deleted out from under the sample record. The FK
        // indexes serve the "where used" view.
        builder.Property(p => p.DiluentLotCode).HasMaxLength(100);
        builder.Property(p => p.NeutralizerLotCode).HasMaxLength(100);
        builder.HasOne(p => p.DiluentMedia).WithMany().HasForeignKey(p => p.DiluentMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.DiluentSolutionPreparation).WithMany().HasForeignKey(p => p.DiluentSolutionPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.DiluentMaterial).WithMany().HasForeignKey(p => p.DiluentMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.NeutralizerMedia).WithMany().HasForeignKey(p => p.NeutralizerMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.NeutralizerSolutionPreparation).WithMany().HasForeignKey(p => p.NeutralizerSolutionPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.NeutralizerMaterial).WithMany().HasForeignKey(p => p.NeutralizerMaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
