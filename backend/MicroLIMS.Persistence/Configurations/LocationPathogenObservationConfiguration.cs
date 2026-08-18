using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class LocationPathogenObservationConfiguration : IEntityTypeConfiguration<LocationPathogenObservation>
{
    public void Configure(EntityTypeBuilder<LocationPathogenObservation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.RowVersion).IsRowVersion();

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
