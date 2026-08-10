using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkflowStepResultConfiguration : IEntityTypeConfiguration<WorkflowStepResult>
{
    public void Configure(EntityTypeBuilder<WorkflowStepResult> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.StepName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ReturnReason).HasMaxLength(1000);

        // Not unique: a BiochemicalTest result deliberately shares the
        // confirmatory step's incubation, since it has no window of its own.
        builder.HasIndex(r => r.IncubationId);

        builder.HasOne(r => r.Incubation)
            .WithMany()
            .HasForeignKey(r => r.IncubationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TestOrder)
            .WithMany()
            .HasForeignKey(r => r.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
