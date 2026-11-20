using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CompetencySectionSettingConfiguration : IEntityTypeConfiguration<CompetencySectionSetting>
{
    public void Configure(EntityTypeBuilder<CompetencySectionSetting> builder)
    {
        builder.ToTable("CompetencySectionSettings");
        builder.HasKey(s => s.SectionId);
        builder.Property(s => s.SectionId).ValueGeneratedNever();
        builder.Property(s => s.GateEnabled).HasDefaultValue(false).IsRequired();
        builder.Property(s => s.ChangedAt).IsRequired();

        builder.HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ChangedByUser)
            .WithMany()
            .HasForeignKey(s => s.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ChangedByUserId);
    }
}
