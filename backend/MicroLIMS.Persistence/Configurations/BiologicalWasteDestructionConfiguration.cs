using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class BiologicalWasteDestructionConfiguration : IEntityTypeConfiguration<BiologicalWasteDestruction>
{
    public void Configure(EntityTypeBuilder<BiologicalWasteDestruction> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(d => d.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.SectionId);

        // Destruction records are permanent GMP history - never cascade
        // from equipment, program or signature deletions.
        builder.HasOne(d => d.AutoclaveEquipment).WithMany().HasForeignKey(d => d.AutoclaveEquipmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.AutoclaveProgram).WithMany().HasForeignKey(d => d.AutoclaveProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Signature).WithMany().HasForeignKey(d => d.SignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.TemperatureSnapshot).HasPrecision(5, 2);
        builder.Property(d => d.ProgramCodeSnapshot).HasMaxLength(50);
        builder.Property(d => d.ProgramNameSnapshot).HasMaxLength(200);
        builder.Property(d => d.LoadTypeSnapshot).HasMaxLength(100);
        builder.Property(d => d.CycleNumber).HasMaxLength(50);
        builder.Property(d => d.Notes).HasMaxLength(2000);
    }
}

public class BiologicalWasteDestructionItemConfiguration : IEntityTypeConfiguration<BiologicalWasteDestructionItem>
{
    public void Configure(EntityTypeBuilder<BiologicalWasteDestructionItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasOne(i => i.BiologicalWasteDestruction).WithMany(d => d.Items)
            .HasForeignKey(i => i.BiologicalWasteDestructionId).OnDelete(DeleteBehavior.Cascade);

        // Each thawed vial, and each withdrawn batch's remaining vials,
        // can be destroyed only once - enforced by the database too, so
        // two destruction records saved at the same moment cannot both
        // claim the same vial.
        builder.HasOne(i => i.ThawEvent).WithOne(t => t.DestructionItem)
            .HasForeignKey<BiologicalWasteDestructionItem>(i => i.ThawEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.ThawEventId).IsUnique();
        builder.HasOne(i => i.Cryovial).WithMany()
            .HasForeignKey(i => i.CryovialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.CryovialId).IsUnique();

        builder.Property(i => i.Quantity).HasPrecision(12, 3);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.Unit).HasMaxLength(50);
    }
}
