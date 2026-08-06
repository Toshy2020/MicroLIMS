using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IncubationConfiguration : IEntityTypeConfiguration<Incubation>
{
    public void Configure(EntityTypeBuilder<Incubation> builder)
    {
        builder.HasKey(i => i.Id);

        // Deleting a Media lot or Equipment record can never cascade-delete
        // incubation history - it's part of the audit trail.
        builder.HasOne(i => i.Media).WithMany().HasForeignKey(i => i.MediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.IncubatorEquipment).WithMany().HasForeignKey(i => i.IncubatorEquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
