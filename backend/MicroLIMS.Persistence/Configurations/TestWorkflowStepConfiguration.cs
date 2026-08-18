using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepConfiguration : IEntityTypeConfiguration<TestWorkflowStep>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StepName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.TemperatureMin).HasColumnType("decimal(5,2)");
        builder.Property(s => s.TemperatureMax).HasColumnType("decimal(5,2)");
        builder.Property(s => s.ConfirmatoryMediaCount).HasDefaultValue(1);

        builder.HasIndex(s => new { s.TestDefinitionId, s.StepOrder }).IsUnique();

        builder.HasOne(s => s.TestDefinition)
            .WithMany(t => t.Steps)
            .HasForeignKey(s => s.TestDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.MediaType)
            .WithMany()
            .HasForeignKey(s => s.MediaTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.TargetOrganism)
            .WithMany()
            .HasForeignKey(s => s.TargetOrganismId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(s => s.RequiresTargetOrganism);
        builder.Ignore(s => s.RequiresIncubationLock);
    }
}
