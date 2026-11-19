using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class UserMfaCredentialConfiguration : IEntityTypeConfiguration<UserMfaCredential>
{
    public void Configure(EntityTypeBuilder<UserMfaCredential> builder)
    {
        builder.ToTable("UserMfaCredentials");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.SecretProtected).IsRequired().HasMaxLength(512);
        builder.Property(c => c.SerialNumber).HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.RevokeReason).HasMaxLength(500);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AssignedByUser)
            .WithMany()
            .HasForeignKey(c => c.AssignedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.RevokedByUser)
            .WithMany()
            .HasForeignKey(c => c.RevokedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.UserId, "IX_UserMfaCredentials_UserId_Active")
            .IsUnique()
            .HasFilter("\"Status\" = 1");
        builder.HasIndex(c => c.UserId, "IX_UserMfaCredentials_UserId_Pending")
            .IsUnique()
            .HasFilter("\"Status\" = 0");
    }
}
