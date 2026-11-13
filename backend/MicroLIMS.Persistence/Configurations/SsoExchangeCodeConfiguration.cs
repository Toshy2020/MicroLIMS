using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SsoExchangeCodeConfiguration : IEntityTypeConfiguration<SsoExchangeCode>
{
    public void Configure(EntityTypeBuilder<SsoExchangeCode> builder)
    {
        builder.ToTable("SsoExchangeCodes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.CodeHash).IsUnique();
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.ExpiresAt);
    }
}
