using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialMasterEntryConfiguration : IEntityTypeConfiguration<MaterialMasterEntry>
{
    public void Configure(EntityTypeBuilder<MaterialMasterEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Grade).HasMaxLength(100);
        builder.Property(e => e.Source).HasMaxLength(150);
        builder.Property(e => e.WorkingConcentration).HasMaxLength(100);
        builder.Property(e => e.Solvent).HasMaxLength(100);
        builder.Property(e => e.ColourChange).HasMaxLength(100);
        builder.Property(e => e.IndicatorUse).HasMaxLength(100);
        builder.Property(e => e.TransitionRangeFrom).HasColumnType("decimal(5,2)");
        builder.Property(e => e.TransitionRangeTo).HasColumnType("decimal(5,2)");
        builder.HasIndex(e => new { e.SectionId, e.Code }).IsUnique();
        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
