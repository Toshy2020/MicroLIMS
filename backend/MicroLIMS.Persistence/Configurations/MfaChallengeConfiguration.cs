using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("MfaChallenges");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Purpose).HasConversion<int>();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.UserId);
    }
}
