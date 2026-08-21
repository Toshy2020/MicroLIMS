using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AdminPasswordRecoveryConfiguration : IEntityTypeConfiguration<AdminPasswordRecovery>
{
    public void Configure(EntityTypeBuilder<AdminPasswordRecovery> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>();

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
