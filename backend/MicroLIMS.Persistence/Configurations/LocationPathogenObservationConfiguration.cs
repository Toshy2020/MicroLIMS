using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class LocationPathogenObservationConfiguration : IEntityTypeConfiguration<LocationPathogenObservation>
{
    public void Configure(EntityTypeBuilder<LocationPathogenObservation> builder)
    {
        builder.HasKey(o => o.Id);

        // Not a real optimistic-concurrency token: IsRowVersion() expects
        // the database to auto-generate a new value on every write (SQL
        // Server's native rowversion type), which Postgres has no
        // equivalent for on a plain bytea column - EF never sent a value,
        // and the NOT NULL column rejected every insert. Nothing in the
        // app reads RowVersion, so it's just an app-managed column now.
        builder.Property(o => o.RowVersion);

        builder.HasOne(o => o.SampleLocation)
            .WithMany()
            .HasForeignKey(o => o.SampleLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.TestOrder)
            .WithMany()
            .HasForeignKey(o => o.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.ObservedByUser)
            .WithMany()
            .HasForeignKey(o => o.ObservedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.SampleLocationId);
        builder.HasIndex(o => o.TestOrderId);
        builder.HasIndex(o => new { o.SampleLocationId, o.TestOrderId });
    }
}
