using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepMediaConfiguration : IEntityTypeConfiguration<TestWorkflowStepMedia>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStepMedia> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.TempMin).HasColumnType("decimal(5,2)");
        builder.Property(m => m.TempMax).HasColumnType("decimal(5,2)");

        // The same media product cannot be listed twice on one step. Rows the
        // backfill could not resolve have no product and are not constrained.
        builder.HasIndex(m => new { m.TestWorkflowStepId, m.MediaProductId }).IsUnique();

        builder.HasOne(m => m.TestWorkflowStep)
            .WithMany(s => s.StepMedia)
            .HasForeignKey(m => m.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.MediaProduct)
            .WithMany()
            .HasForeignKey(m => m.MediaProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Legacy stock-lot link, kept for history only.
        builder.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.IncubationCondition)
            .WithMany()
            .HasForeignKey(m => m.MediaIncubationConditionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
