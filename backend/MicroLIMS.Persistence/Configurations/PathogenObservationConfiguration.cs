using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class PathogenObservationConfiguration : IEntityTypeConfiguration<PathogenObservation>
{
    public void Configure(EntityTypeBuilder<PathogenObservation> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.PlateLabel).HasMaxLength(20);

        // Same reasoning as Incubation.Media - deleting a Media lot can
        // never cascade-delete the observation it was used for.
        builder.HasOne(o => o.Media).WithMany().HasForeignKey(o => o.MediaId).OnDelete(DeleteBehavior.Restrict);
    }
}
