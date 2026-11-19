using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SectionSelfAssignmentSettingConfiguration : IEntityTypeConfiguration<SectionSelfAssignmentSetting>
{
    public void Configure(EntityTypeBuilder<SectionSelfAssignmentSetting> builder)
    {
        builder.ToTable("SectionSelfAssignmentSettings");
        builder.HasKey(s => s.SectionId);
        builder.Property(s => s.SectionId).ValueGeneratedNever();
        builder.Property(s => s.Enabled).IsRequired();
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
