using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaTypeConfiguration : IEntityTypeConfiguration<MediaType>
{
    public void Configure(EntityTypeBuilder<MediaType> builder)
    {
        builder.HasKey(m => m.Id);

        // Fixed set - exactly one row per MediaClass value, enforced at
        // the DB level.
        builder.HasIndex(m => m.Class).IsUnique();
    }
}
