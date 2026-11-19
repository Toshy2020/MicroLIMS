using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> builder)
    {
        builder.Property(r => r.InvalidationReason).HasMaxLength(500);

        // An invalidated result is kept for the record but is no longer a result of
        // its test order: every normal read (workspace, review, approval, reports,
        // counts) skips it without each query having to remember to.
        builder.HasQueryFilter(r => r.InvalidatedAt == null);
    }
}
