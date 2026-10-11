using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaStorageEntryConfiguration : IEntityTypeConfiguration<MediaStorageEntry>
{
    public void Configure(EntityTypeBuilder<MediaStorageEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Form)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.LocationText)
            .HasMaxLength(200);

        builder.HasOne(e => e.Media)
            .WithMany(m => m.StorageEntries)
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Equipment)
            .WithMany()
            .HasForeignKey(e => e.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MediaId, e.Form, e.EquipmentId, e.LocationText })
            .IsUnique();
    }
}
