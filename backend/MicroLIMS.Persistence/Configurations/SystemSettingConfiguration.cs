using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Value).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(s => s.Key).IsUnique();
    }
}

public class SystemSettingVersionConfiguration : IEntityTypeConfiguration<SystemSettingVersion>
{
    public void Configure(EntityTypeBuilder<SystemSettingVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Value).IsRequired();
        builder.Property(v => v.PreviousValue).IsRequired();
        builder.Property(v => v.Reason).HasMaxLength(500).IsRequired();

        builder.HasIndex(v => new { v.SettingId, v.Version }).IsUnique();

        builder.HasOne(v => v.Setting).WithMany(s => s.Versions)
            .HasForeignKey(v => v.SettingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(v => v.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.ElectronicSignature).WithMany()
            .HasForeignKey(v => v.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}
