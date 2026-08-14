using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepIncubationStageConfiguration : IEntityTypeConfiguration<TestWorkflowStepIncubationStage>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStepIncubationStage> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TempMin).HasColumnType("decimal(5,2)");
        builder.Property(s => s.TempMax).HasColumnType("decimal(5,2)");

        // One row per stage number per step - "stage 2 defined twice" is
        // a config error, not a valid multi-row state.
        builder.HasIndex(s => new { s.TestWorkflowStepId, s.StageNumber }).IsUnique();

        builder.HasOne(s => s.TestWorkflowStep)
            .WithMany(t => t.IncubationStages)
            .HasForeignKey(s => s.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
