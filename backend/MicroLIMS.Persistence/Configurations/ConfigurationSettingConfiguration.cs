using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ConfigurationSettingConfiguration : IEntityTypeConfiguration<ConfigurationSetting>
{
    public void Configure(EntityTypeBuilder<ConfigurationSetting> builder)
    {
        builder.ToTable("ConfigurationSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SettingKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.SettingValue)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(s => s.DataType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.SettingGroup)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(s => s.ModifiedByUser)
            .WithMany()
            .HasForeignKey(s => s.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.SettingKey)
            .IsUnique();
    }
}
