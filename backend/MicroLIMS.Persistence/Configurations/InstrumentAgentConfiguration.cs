using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class InstrumentAgentConfiguration : IEntityTypeConfiguration<InstrumentAgent>
{
    public void Configure(EntityTypeBuilder<InstrumentAgent> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(a => a.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.KeyPrefix).IsRequired().HasMaxLength(16);
        builder.Property(a => a.RevokeReason).HasMaxLength(500);

        builder.HasOne(a => a.Equipment)
            .WithMany()
            .HasForeignKey(a => a.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Section)
            .WithMany()
            .HasForeignKey(a => a.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Code).IsUnique();
        builder.HasIndex(a => a.KeyHash).IsUnique();
        builder.HasIndex(a => a.EquipmentId);
        builder.HasIndex(a => a.SectionId);
    }
}
