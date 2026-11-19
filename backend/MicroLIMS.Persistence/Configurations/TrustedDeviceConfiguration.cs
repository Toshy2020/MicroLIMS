using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("TrustedDevices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(d => d.CreatedIp).HasMaxLength(RefreshToken.MaxIpLength);
        builder.Property(d => d.UserAgent).HasMaxLength(RefreshToken.MaxUserAgentLength);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.RevokedByUser)
            .WithMany()
            .HasForeignKey(d => d.RevokedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.TokenHash).IsUnique();
        builder.HasIndex(d => d.UserId);
    }
}
