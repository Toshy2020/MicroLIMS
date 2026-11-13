using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class UserUiStateConfiguration : IEntityTypeConfiguration<UserUiState>
{
    public void Configure(EntityTypeBuilder<UserUiState> builder)
    {
        builder.ToTable("UserUiStates");

        builder.HasKey(s => new { s.UserId, s.Key });
        builder.Property(s => s.Key).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(2048);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
