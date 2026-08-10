using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ConfirmatoryPlateObservationConfiguration : IEntityTypeConfiguration<ConfirmatoryPlateObservation>
{
    public void Configure(EntityTypeBuilder<ConfirmatoryPlateObservation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.WorkflowStepResultId, o.MaterialId }).IsUnique();

        builder.HasOne(o => o.WorkflowStepResult)
            .WithMany(r => r.ConfirmatoryObservations)
            .HasForeignKey(o => o.WorkflowStepResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Material)
            .WithMany()
            .HasForeignKey(o => o.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
