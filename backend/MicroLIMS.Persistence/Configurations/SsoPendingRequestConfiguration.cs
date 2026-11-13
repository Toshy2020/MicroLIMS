using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SsoPendingRequestConfiguration : IEntityTypeConfiguration<SsoPendingRequest>
{
    public void Configure(EntityTypeBuilder<SsoPendingRequest> builder)
    {
        builder.ToTable("SsoPendingRequests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.StateHash).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Nonce).IsRequired().HasMaxLength(128);
        builder.Property(r => r.CodeVerifier).IsRequired().HasMaxLength(256);

        builder.HasIndex(r => r.StateHash).IsUnique();
        builder.HasIndex(r => r.ExpiresAt);
    }
}
