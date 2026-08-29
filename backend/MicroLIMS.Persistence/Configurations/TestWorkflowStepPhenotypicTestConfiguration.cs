using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepPhenotypicTestConfiguration : IEntityTypeConfiguration<TestWorkflowStepPhenotypicTest>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStepPhenotypicTest> builder)
    {
        builder.HasKey(t => t.Id);

        // The same phenotypic test type cannot be listed twice on one step.
        builder.HasIndex(t => new { t.TestWorkflowStepId, t.PhenotypicTestType }).IsUnique();

        builder.HasOne(t => t.TestWorkflowStep)
            .WithMany(s => s.PhenotypicTests)
            .HasForeignKey(t => t.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
