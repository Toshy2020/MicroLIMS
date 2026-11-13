using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(r => r.CreatedIp).HasMaxLength(RefreshToken.MaxIpLength);
        builder.Property(r => r.UserAgent).HasMaxLength(RefreshToken.MaxUserAgentLength);
        builder.Property(r => r.MfaVerified).IsRequired().HasDefaultValue(false);
    }
}
