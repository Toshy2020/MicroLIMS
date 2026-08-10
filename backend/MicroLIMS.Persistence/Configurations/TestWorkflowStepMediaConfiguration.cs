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

        // The same medium cannot be listed twice on one step.
        builder.HasIndex(m => new { m.TestWorkflowStepId, m.MaterialId }).IsUnique();

        builder.HasOne(m => m.TestWorkflowStep)
            .WithMany(s => s.StepMedia)
            .HasForeignKey(m => m.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
