using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaIncubationConditionConfiguration : IEntityTypeConfiguration<MediaIncubationCondition>
{
    public void Configure(EntityTypeBuilder<MediaIncubationCondition> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TemperatureMin).HasColumnType("decimal(5,2)");
        builder.Property(m => m.TemperatureMax).HasColumnType("decimal(5,2)");

        builder.HasOne(m => m.MediaProduct)
            .WithMany(p => p.IncubationConditions)
            .HasForeignKey(m => m.MediaProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.MediaProductId, m.IncubationMinHours, m.IncubationMaxHours, m.TemperatureMin, m.TemperatureMax }).IsUnique();
    }
}
